using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MorrusPOS.Application.Features.Auth;
using MorrusPOS.Application.Features.Products;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Application.Features.Transactions;

namespace MorrusPOS.IntegrationTests;

public class PosMainFlowApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public PosMainFlowApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task OpenSession_CheckoutReplay_DeductStockOnce_AndCloseSession()
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        Guid? businessId = null;

        try
        {
            var registration = await _client.PostAsJsonAsync("/api/auth/register-owner", new RegisterOwnerRequest(
                new RegisterOwnerOwnerDetails("POS Test Owner", $"pos-{suffix}@example.test", "Password123!"),
                new RegisterOwnerBusinessDetails($"POS Test {suffix}", "Retail Umum", null, "retail"),
                "Free Trial",
                new RegisterOwnerOutletDetails("Outlet Utama", $"POS-{suffix}", null, null)));
            registration.StatusCode.Should().Be(HttpStatusCode.OK);
            var login = await registration.Content.ReadFromJsonAsync<LoginResponse>();
            login.Should().NotBeNull();
            businessId = login!.BusinessId;
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

            var outlets = await _client.GetFromJsonAsync<List<OutletResponse>>("/api/outlets");
            var outletId = outlets.Should().ContainSingle().Subject.Id;

            var categoryResponse = await _client.PostAsJsonAsync(
                "/api/categories",
                new CreateCategoryRequest("Integration Test", null));
            categoryResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>();

            var productResponse = await _client.PostAsJsonAsync(
                "/api/products",
                new CreateProductRequest(
                    category!.Id,
                    $"SKU-{suffix}",
                    "Produk Integration",
                    null,
                    10_000,
                    7_000,
                    "pcs",
                    false,
                    TrackingMode: "quantity"));
            productResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var product = await productResponse.Content.ReadFromJsonAsync<ProductDto>();

            var opnameResponse = await _client.PostAsJsonAsync(
                "/api/stockopnames",
                new CreateStockOpnameRequest(outletId, [new StockOpnameItemRequest(product!.Id, 5)]));
            opnameResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var stockedProducts = await _client.GetFromJsonAsync<List<ProductDto>>($"/api/products?outletId={outletId}");
            stockedProducts!.Single(x => x.Id == product.Id).QtyOnHand.Should().Be(5);

            var openResponse = await _client.PostAsJsonAsync(
                "/api/cashier-sessions/open",
                new OpenSessionRequest(100_000, outletId));
            openResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var session = await openResponse.Content.ReadFromJsonAsync<CashierSessionDto>();

            var checkoutId = Guid.NewGuid();
            var paymentId = Guid.NewGuid();
            var checkout = new CheckoutRequest(
                checkoutId,
                outletId,
                session!.Id,
                "pos",
                20_000,
                0,
                0,
                20_000,
                [new CheckoutItemRequest(product.Id, 2, 10_000, 0)],
                [new PaymentRequest("cash", 20_000, null, paymentId)]);

            using var firstRequest = BuildCheckoutRequest(checkout);
            var firstCheckout = await _client.SendAsync(firstRequest);
            firstCheckout.StatusCode.Should().Be(HttpStatusCode.Created);
            var transaction = await firstCheckout.Content.ReadFromJsonAsync<TransactionDto>();

            using var replayRequest = BuildCheckoutRequest(checkout);
            var replay = await _client.SendAsync(replayRequest);
            replay.StatusCode.Should().Be(HttpStatusCode.Created);
            var replayedTransaction = await replay.Content.ReadFromJsonAsync<TransactionDto>();
            replayedTransaction!.Id.Should().Be(transaction!.Id);

            var products = await _client.GetFromJsonAsync<List<ProductDto>>($"/api/products?outletId={outletId}");
            products!.Single(x => x.Id == product.Id).QtyOnHand.Should().Be(3);

            var closeResponse = await _client.PostAsJsonAsync(
                $"/api/cashier-sessions/close/{session.Id}",
                new CloseSessionRequest(119_000));
            closeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var closed = await closeResponse.Content.ReadFromJsonAsync<CashierSessionDto>();
            closed!.ExpectedCash.Should().Be(120_000);
            closed.Variance.Should().Be(-1_000);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            if (businessId.HasValue)
                await IntegrationTestDatabase.CleanupBusinessAsync(businessId.Value);
        }
    }

    private static HttpRequestMessage BuildCheckoutRequest(CheckoutRequest checkout)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/transactions/checkout")
        {
            Content = JsonContent.Create(checkout),
        };
        request.Headers.Add("Idempotency-Key", checkout.Id.ToString());
        return request;
    }

    private sealed record OutletResponse(Guid Id);
}
