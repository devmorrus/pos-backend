using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Auth;
using MorrusPOS.Application.Features.Products;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Application.Features.Transactions;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;
using Npgsql;

namespace MorrusPOS.IntegrationTests;

public class CheckoutConcurrencyApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CheckoutConcurrencyApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    [Trait("Category", "Concurrency")]
    public async Task TwoCashiers_SellingLastUnit_Should_CommitExactlyOneCheckout()
    {
        var barrier = new CheckoutStockBarrier();
        using var testFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IStockService>();
                services.AddScoped<IStockService>(serviceProvider => new BarrierStockService(
                    new StockService(
                        serviceProvider.GetRequiredService<AppDbContext>(),
                        serviceProvider.GetRequiredService<ICurrentUserService>()),
                    barrier));
            }));
        using var ownerClient = testFactory.CreateClient();
        using var cashierAClient = testFactory.CreateClient();
        using var cashierBClient = testFactory.CreateClient();
        var suffix = Guid.NewGuid().ToString("N")[..10];
        Guid? businessId = null;

        try
        {
            var registration = await ownerClient.PostAsJsonAsync("/api/auth/register-owner", new RegisterOwnerRequest(
                new RegisterOwnerOwnerDetails("Concurrency Owner", $"concurrency-{suffix}@example.test", "Password123!"),
                new RegisterOwnerBusinessDetails($"Concurrency {suffix}", "Retail Umum", null, "retail"),
                "Free Trial",
                new RegisterOwnerOutletDetails("Outlet Concurrency", $"CON-{suffix}", null, null)));
            registration.StatusCode.Should().Be(HttpStatusCode.OK);
            var owner = await registration.Content.ReadFromJsonAsync<LoginResponse>();
            owner.Should().NotBeNull();
            businessId = owner!.BusinessId;
            ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", owner.AccessToken);

            var outlets = await ownerClient.GetFromJsonAsync<List<OutletResponse>>("/api/outlets");
            var outletId = outlets.Should().ContainSingle().Subject.Id;
            var product = await CreateLastUnitProductAsync(ownerClient, outletId, suffix);

            var cashierAId = Guid.NewGuid();
            var cashierBId = Guid.NewGuid();
            await SeedCashiersAsync(businessId!.Value, outletId, cashierAId, cashierBId, suffix);

            using (var scope = testFactory.Services.CreateScope())
            {
                var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
                cashierAClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    jwt.GenerateAccessToken(cashierAId, outletId, "Kasir", businessId.Value,
                        owner.SubscriptionStatus, owner.TrialEndDate));
                cashierBClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    jwt.GenerateAccessToken(cashierBId, outletId, "Kasir", businessId.Value,
                        owner.SubscriptionStatus, owner.TrialEndDate));
            }

            var sessionA = await OpenSessionAsync(cashierAClient, outletId);
            var sessionB = await OpenSessionAsync(cashierBClient, outletId);
            sessionA.Id.Should().NotBe(sessionB.Id);
            sessionA.UserId.Should().Be(cashierAId);
            sessionB.UserId.Should().Be(cashierBId);

            var checkoutA = BuildCheckout(product, outletId, sessionA.Id);
            var checkoutB = BuildCheckout(product, outletId, sessionB.Id);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

            var checkoutATask = SendAfterBarrierAsync(cashierAClient, checkoutA, start.Task, timeout.Token);
            var checkoutBTask = SendAfterBarrierAsync(cashierBClient, checkoutB, start.Task, timeout.Token);
            start.SetResult();

            try
            {
                await barrier.BothArrived.WaitAsync(timeout.Token);
            }
            finally
            {
                barrier.Release();
            }

            using var responseA = await checkoutATask;
            using var responseB = await checkoutBTask;
            var responses = new[] { responseA, responseB };
            responses.Count(response => response.StatusCode == HttpStatusCode.Created).Should().Be(1);
            responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest).Should().Be(1);
            responses.Should().NotContain(response => response.StatusCode == HttpStatusCode.InternalServerError);

            var success = responses.Single(response => response.StatusCode == HttpStatusCode.Created);
            var failure = responses.Single(response => response.StatusCode == HttpStatusCode.BadRequest);
            var transaction = await success.Content.ReadFromJsonAsync<TransactionDto>();
            transaction.Should().NotBeNull();
            new[] { checkoutA.Id, checkoutB.Id }.Should().Contain(transaction!.Id);

            using (var error = JsonDocument.Parse(await failure.Content.ReadAsStringAsync()))
                error.RootElement.GetProperty("code").GetString().Should().Be("STOCK_MOVEMENT_INVALID");

            await AssertCommittedStateAsync(
                businessId.Value, outletId, product.Id, checkoutA.Id, checkoutB.Id,
                sessionA.Id, sessionB.Id, transaction.Id, timeout.Token);
        }
        finally
        {
            if (businessId.HasValue)
                await IntegrationTestDatabase.CleanupBusinessAsync(businessId.Value);
        }
    }

    private static async Task<ProductDto> CreateLastUnitProductAsync(HttpClient ownerClient, Guid outletId, string suffix)
    {
        var categoryResponse = await ownerClient.PostAsJsonAsync(
            "/api/categories", new CreateCategoryRequest($"Concurrency {suffix}", null));
        categoryResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>();

        var productResponse = await ownerClient.PostAsJsonAsync(
            "/api/products",
            new CreateProductRequest(
                category!.Id, $"LAST-{suffix}", "Unit Terakhir", null,
                10_000, 7_000, "pcs", false, TrackingMode: "quantity"));
        productResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var product = await productResponse.Content.ReadFromJsonAsync<ProductDto>();

        var opname = await ownerClient.PostAsJsonAsync(
            "/api/stockopnames",
            new CreateStockOpnameRequest(outletId, [new StockOpnameItemRequest(product!.Id, 1)]));
        opname.StatusCode.Should().Be(HttpStatusCode.Created);

        var products = await ownerClient.GetFromJsonAsync<List<ProductDto>>($"/api/products?outletId={outletId}");
        products!.Single(current => current.Id == product.Id).QtyOnHand.Should().Be(1);
        return product;
    }

    private static async Task SeedCashiersAsync(
        Guid businessId, Guid outletId, Guid cashierAId, Guid cashierBId, string suffix)
    {
        await using var connection = new NpgsqlConnection(
            IntegrationTestDatabase.RequireConnectionString("MORRUS_TENANCY_TEST_ADMIN"));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO users
                ("Id","BusinessId","OutletId","RoleId","Name","Email","PasswordHash","IsActive","CreatedAt","UpdatedAt")
            SELECT @cashier_a,@business,@outlet,"Id",'Kasir A',@email_a,'test',true,now(),now()
            FROM roles WHERE "Name"='Kasir';
            INSERT INTO users
                ("Id","BusinessId","OutletId","RoleId","Name","Email","PasswordHash","IsActive","CreatedAt","UpdatedAt")
            SELECT @cashier_b,@business,@outlet,"Id",'Kasir B',@email_b,'test',true,now(),now()
            FROM roles WHERE "Name"='Kasir';
            """;
        command.Parameters.AddWithValue("cashier_a", cashierAId);
        command.Parameters.AddWithValue("cashier_b", cashierBId);
        command.Parameters.AddWithValue("business", businessId);
        command.Parameters.AddWithValue("outlet", outletId);
        command.Parameters.AddWithValue("email_a", $"cashier-a-{suffix}@example.test");
        command.Parameters.AddWithValue("email_b", $"cashier-b-{suffix}@example.test");
        (await command.ExecuteNonQueryAsync()).Should().Be(2);
    }

    private static async Task<CashierSessionDto> OpenSessionAsync(HttpClient client, Guid outletId)
    {
        var response = await client.PostAsJsonAsync(
            "/api/cashier-sessions/open", new OpenSessionRequest(0, outletId));
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<CashierSessionDto>())!;
    }

    private static CheckoutRequest BuildCheckout(ProductDto product, Guid outletId, Guid sessionId)
        => new(
            Guid.NewGuid(), outletId, sessionId, "pos",
            product.BasePrice, 0, 0, product.BasePrice,
            [new CheckoutItemRequest(product.Id, 1, product.BasePrice, 0)],
            [new PaymentRequest("cash", product.BasePrice, null, Guid.NewGuid())]);

    private static async Task<HttpResponseMessage> SendAfterBarrierAsync(
        HttpClient client,
        CheckoutRequest checkout,
        Task barrier,
        CancellationToken ct)
    {
        await barrier.WaitAsync(ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/transactions/checkout")
        {
            Content = JsonContent.Create(checkout),
        };
        request.Headers.Add("Idempotency-Key", checkout.Id.ToString());
        return await client.SendAsync(request, ct);
    }

    private static async Task AssertCommittedStateAsync(
        Guid businessId,
        Guid outletId,
        Guid productId,
        Guid checkoutAId,
        Guid checkoutBId,
        Guid sessionAId,
        Guid sessionBId,
        Guid winnerId,
        CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(
            IntegrationTestDatabase.RequireConnectionString("MORRUS_TENANCY_TEST_ADMIN"));
        await connection.OpenAsync(ct);

        (await ScalarAsync<decimal>(connection, """
            SELECT "QtyOnHand" FROM inventory_stock
            WHERE "BusinessId"=@business AND "StockLocationId"=@outlet AND "ProductId"=@product
              AND "ProductVariantId" IS NULL;
            """, businessId, outletId, productId, checkoutAId, checkoutBId, ct)).Should().Be(0);

        (await ScalarAsync<int>(connection, """
            SELECT count(*)::int FROM transactions
            WHERE "BusinessId"=@business AND "Id" IN (@checkout_a,@checkout_b);
            """, businessId, outletId, productId, checkoutAId, checkoutBId, ct)).Should().Be(1);

        (await ScalarAsync<int>(connection, """
            SELECT count(*)::int FROM transaction_items
            WHERE "BusinessId"=@business AND "TransactionId" IN (@checkout_a,@checkout_b) AND "Qty"=1;
            """, businessId, outletId, productId, checkoutAId, checkoutBId, ct)).Should().Be(1);

        (await ScalarAsync<int>(connection, """
            SELECT count(*)::int FROM payments
            WHERE "BusinessId"=@business AND "TransactionId" IN (@checkout_a,@checkout_b);
            """, businessId, outletId, productId, checkoutAId, checkoutBId, ct)).Should().Be(1);

        await using (var sessions = connection.CreateCommand())
        {
            sessions.CommandText = """
                SELECT count(*)::int FROM cashier_sessions
                WHERE "BusinessId"=@business AND "Id" IN (@session_a,@session_b) AND "Status"='open';
                """;
            sessions.Parameters.AddWithValue("business", businessId);
            sessions.Parameters.AddWithValue("session_a", sessionAId);
            sessions.Parameters.AddWithValue("session_b", sessionBId);
            ((int)(await sessions.ExecuteScalarAsync(ct))!).Should().Be(2);
        }

        await using (var winnerScope = connection.CreateCommand())
        {
            winnerScope.CommandText = """
                SELECT count(*)::int
                FROM transactions trx
                JOIN cashier_sessions session ON session."Id"=trx."CashierSessionId"
                WHERE trx."BusinessId"=@business AND trx."Id"=@winner
                  AND trx."UserId"=session."UserId"
                  AND trx."CashierSessionId" IN (@session_a,@session_b);
                """;
            winnerScope.Parameters.AddWithValue("business", businessId);
            winnerScope.Parameters.AddWithValue("winner", winnerId);
            winnerScope.Parameters.AddWithValue("session_a", sessionAId);
            winnerScope.Parameters.AddWithValue("session_b", sessionBId);
            ((int)(await winnerScope.ExecuteScalarAsync(ct))!).Should().Be(1);
        }

        await using (var ledger = connection.CreateCommand())
        {
            ledger.CommandText = """
                SELECT count(*)::int,coalesce(sum("QtyChange"),0)
                FROM stock_ledger
                WHERE "BusinessId"=@business AND "ReferenceType"='transaction'
                  AND "ReferenceId" IN (@checkout_a,@checkout_b) AND "MovementType"='sale';
                """;
            AddCommonParameters(ledger, businessId, outletId, productId, checkoutAId, checkoutBId);
            await using var reader = await ledger.ExecuteReaderAsync(ct);
            (await reader.ReadAsync(ct)).Should().BeTrue();
            reader.GetInt32(0).Should().Be(1);
            reader.GetDecimal(1).Should().Be(-1);
        }

        (await ScalarAsync<int>(connection, """
            SELECT count(*)::int FROM idempotency_records
            WHERE "BusinessId"=@business AND "Operation"='checkout'
              AND "IdempotencyKey" IN (@checkout_a,@checkout_b);
            """, businessId, outletId, productId, checkoutAId, checkoutBId, ct)).Should().Be(1);

        await using (var journal = connection.CreateCommand())
        {
            journal.CommandText = """
                SELECT count(*)::int,coalesce(sum("TotalDebit"-"TotalCredit"),0)
                FROM journal_entries
                WHERE "BusinessId"=@business AND "ReferenceType"='transaction_sale'
                  AND "ReferenceId" IN (@checkout_a,@checkout_b);
                """;
            AddCommonParameters(journal, businessId, outletId, productId, checkoutAId, checkoutBId);
            await using var reader = await journal.ExecuteReaderAsync(ct);
            (await reader.ReadAsync(ct)).Should().BeTrue();
            reader.GetInt32(0).Should().Be(1);
            reader.GetDecimal(1).Should().Be(0);
        }

        (await ScalarAsync<int>(connection, """
            SELECT count(*)::int FROM account_transactions
            WHERE "BusinessId"=@business AND "ReferenceType"='transaction_sale'
              AND "ReferenceId"=@winner;
            """, businessId, outletId, productId, checkoutAId, checkoutBId, ct, winnerId)).Should().BeGreaterThanOrEqualTo(2);
    }

    private static async Task<T> ScalarAsync<T>(
        NpgsqlConnection connection,
        string sql,
        Guid businessId,
        Guid outletId,
        Guid productId,
        Guid checkoutAId,
        Guid checkoutBId,
        CancellationToken ct,
        Guid? winnerId = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddCommonParameters(command, businessId, outletId, productId, checkoutAId, checkoutBId);
        if (winnerId.HasValue)
            command.Parameters.AddWithValue("winner", winnerId.Value);
        return (T)(await command.ExecuteScalarAsync(ct))!;
    }

    private static void AddCommonParameters(
        NpgsqlCommand command,
        Guid businessId,
        Guid outletId,
        Guid productId,
        Guid checkoutAId,
        Guid checkoutBId)
    {
        command.Parameters.AddWithValue("business", businessId);
        command.Parameters.AddWithValue("outlet", outletId);
        command.Parameters.AddWithValue("product", productId);
        command.Parameters.AddWithValue("checkout_a", checkoutAId);
        command.Parameters.AddWithValue("checkout_b", checkoutBId);
    }

    private sealed class CheckoutStockBarrier
    {
        private readonly TaskCompletionSource _bothArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;

        public Task BothArrived => _bothArrived.Task;

        public async Task ArriveAndWaitAsync(CancellationToken ct)
        {
            if (Interlocked.Increment(ref _arrivals) == 2)
                _bothArrived.TrySetResult();
            await _release.Task.WaitAsync(ct);
        }

        public void Release() => _release.TrySetResult();
    }

    private sealed class BarrierStockService : IStockService
    {
        private readonly IStockService _inner;
        private readonly CheckoutStockBarrier _barrier;

        public BarrierStockService(IStockService inner, CheckoutStockBarrier barrier)
        {
            _inner = inner;
            _barrier = barrier;
        }

        public async Task AddMovementAsync(
            Guid productId,
            Guid outletId,
            decimal qtyChange,
            string movementType,
            string referenceType,
            Guid referenceId,
            string? note = null,
            CancellationToken ct = default,
            Guid? productVariantId = null,
            Guid? productBatchId = null,
            Guid? productSerialId = null,
            Guid? documentItemId = null,
            Guid? operationId = null)
        {
            if (movementType == "sale")
                await _barrier.ArriveAndWaitAsync(ct);
            await _inner.AddMovementAsync(
                productId, outletId, qtyChange, movementType, referenceType, referenceId, note, ct,
                productVariantId, productBatchId, productSerialId, documentItemId, operationId);
        }
    }

    private sealed record OutletResponse(Guid Id);
}
