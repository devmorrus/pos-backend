using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MorrusPOS.Application.Features.Auth;
using MorrusPOS.Application.Features.Products;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Application.Features.Transactions;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.IntegrationTests;

public class StockReservationApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public StockReservationApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ReserveThenCheckout_Should_MoveReservedToSoldAtomically()
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        Guid? businessId = null;
        try
        {
            var registration = await _client.PostAsJsonAsync("/api/auth/register-owner", new RegisterOwnerRequest(
                new RegisterOwnerOwnerDetails("Reservation Owner", $"reservation-{suffix}@example.test", "Password123!"),
                new RegisterOwnerBusinessDetails($"Reservation {suffix}", "Retail Umum", null, "retail"),
                "Free Trial",
                new RegisterOwnerOutletDetails("Outlet Reservation", $"RES-{suffix}", null, null)));
            registration.StatusCode.Should().Be(HttpStatusCode.OK);
            var login = await registration.Content.ReadFromJsonAsync<LoginResponse>();
            businessId = login!.BusinessId;
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

            var outlets = await _client.GetFromJsonAsync<List<OutletResponse>>("/api/outlets");
            var outletId = outlets.Should().ContainSingle().Subject.Id;
            var categoryResponse = await _client.PostAsJsonAsync("/api/categories", new CreateCategoryRequest("Reservation", null));
            var category = await categoryResponse.Content.ReadFromJsonAsync<CategoryDto>();
            var productResponse = await _client.PostAsJsonAsync("/api/products", new CreateProductRequest(
                category!.Id, $"RES-{suffix}", "Produk Reservation", null, 10_000, 7_000, "pcs", false,
                TrackingMode: ProductTrackingModes.Quantity));
            var product = await productResponse.Content.ReadFromJsonAsync<ProductDto>();
            (await _client.PostAsJsonAsync("/api/stockopnames", new CreateStockOpnameRequest(
                outletId, [new StockOpnameItemRequest(product!.Id, 2)]))).StatusCode.Should().Be(HttpStatusCode.Created);
            var sessionResponse = await _client.PostAsJsonAsync("/api/cashier-sessions/open", new OpenSessionRequest(0, outletId));
            var session = await sessionResponse.Content.ReadFromJsonAsync<CashierSessionDto>();

            var checkoutId = Guid.NewGuid();
            var reservationRequest = new CreateStockReservationRequest(
                checkoutId, outletId, session!.Id, [new StockReservationItemRequest(product.Id, 2)]);
            using var reserveMessage = new HttpRequestMessage(HttpMethod.Post, "/api/stock-reservations")
            {
                Content = JsonContent.Create(reservationRequest)
            };
            reserveMessage.Headers.Add("Idempotency-Key", checkoutId.ToString());
            var reserveResponse = await _client.SendAsync(reserveMessage);
            reserveResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var reservedInventory = await _client.GetFromJsonAsync<List<InventoryListItemDto>>(
                $"/api/inventory?stockLocationId={outletId}");
            var reservedRow = reservedInventory!.Single(x => x.ProductId == product.Id);
            reservedRow.QtyOnHand.Should().Be(2);
            reservedRow.QtyReserved.Should().Be(2);
            reservedRow.QtyAvailable.Should().Be(0);

            var losingReservationId = Guid.NewGuid();
            using var losingMessage = new HttpRequestMessage(HttpMethod.Post, "/api/stock-reservations")
            {
                Content = JsonContent.Create(new CreateStockReservationRequest(
                    losingReservationId, outletId, session.Id,
                    [new StockReservationItemRequest(product.Id, 1)]))
            };
            losingMessage.Headers.Add("Idempotency-Key", losingReservationId.ToString());
            var losingResponse = await _client.SendAsync(losingMessage);
            losingResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

            var checkout = new CheckoutRequest(
                checkoutId, outletId, session.Id, TransactionChannel.Pos,
                20_000, 0, 0, 20_000,
                [new CheckoutItemRequest(product.Id, 2, 10_000, 0)],
                [new PaymentRequest(PaymentMethod.Cash, 20_000, null, Guid.NewGuid())]);
            using var checkoutMessage = new HttpRequestMessage(HttpMethod.Post, "/api/transactions/checkout")
            {
                Content = JsonContent.Create(checkout)
            };
            checkoutMessage.Headers.Add("Idempotency-Key", checkoutId.ToString());
            var checkoutResponse = await _client.SendAsync(checkoutMessage);
            checkoutResponse.StatusCode.Should().Be(HttpStatusCode.Created);

            var soldInventory = await _client.GetFromJsonAsync<List<InventoryListItemDto>>(
                $"/api/inventory?stockLocationId={outletId}");
            var soldRow = soldInventory!.Single(x => x.ProductId == product.Id);
            soldRow.QtyOnHand.Should().Be(0);
            soldRow.QtyReserved.Should().Be(0);
            soldRow.QtyAvailable.Should().Be(0);
            soldRow.QtySold.Should().Be(2);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            if (businessId.HasValue)
                await IntegrationTestDatabase.CleanupBusinessAsync(businessId.Value);
        }
    }

    private sealed record OutletResponse(Guid Id);
}
