using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Events;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Application.Features.Transactions;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;
using NSubstitute;

namespace MorrusPOS.UnitTests;

public class StockReservationServiceTests
{
    [Fact]
    public async Task CreateRelease_Should_ChangeAvailabilityWithoutChangingPhysicalStock()
    {
        var fixture = await Fixture.CreateAsync();
        var reservationId = Guid.NewGuid();
        var serviceProductId = Guid.NewGuid();
        fixture.Db.Products.Add(new Product
        {
            Id = serviceProductId,
            CategoryId = await fixture.Db.Categories.Select(x => x.Id).SingleAsync(),
            Sku = "SERVICE-1", Name = "Jasa", BasePrice = 5_000, CostPrice = 0,
            Unit = "jasa", IsActive = true, TrackingMode = ProductTrackingModes.None
        });
        await fixture.Db.SaveChangesAsync();

        var created = await fixture.Reservations.CreateAsync(new CreateStockReservationRequest(
            reservationId, fixture.OutletId, fixture.SessionId,
            [new StockReservationItemRequest(fixture.ProductId, 2), new StockReservationItemRequest(serviceProductId, 1)]));

        created.Status.Should().Be(StockReservationStatus.Active);
        created.Items.Should().ContainSingle(x => x.ProductId == fixture.ProductId);
        (await fixture.Db.InventoryStocks.SingleAsync()).QtyOnHand.Should().Be(5);
        (await fixture.Availability.GetReservedQuantityAsync(
            fixture.OutletId, fixture.ProductId, null)).Should().Be(2);
        var inventory = await new InventoryService(fixture.Db, fixture.Availability)
            .GetByStockLocationAsync(fixture.OutletId);
        inventory.Single().QtyOnHand.Should().Be(5);
        inventory.Single().QtyReserved.Should().Be(2);
        inventory.Single().QtyAvailable.Should().Be(3);
        fixture.Db.StockLedgers.Should().BeEmpty();

        var released = await fixture.Reservations.ReleaseAsync(reservationId, "Kasir kembali ke katalog.");
        released.Status.Should().Be(StockReservationStatus.Released);
        (await fixture.Availability.GetReservedQuantityAsync(
            fixture.OutletId, fixture.ProductId, null)).Should().Be(0);
        (await fixture.Db.InventoryStocks.SingleAsync()).QtyOnHand.Should().Be(5);
    }

    [Fact]
    public async Task Checkout_Should_ConsumeReservationAndPostSaleOnce()
    {
        var fixture = await Fixture.CreateAsync();
        var checkoutId = Guid.NewGuid();
        await fixture.Reservations.CreateAsync(new CreateStockReservationRequest(
            checkoutId, fixture.OutletId, fixture.SessionId,
            [new StockReservationItemRequest(fixture.ProductId, 2)]));

        var transactions = new TransactionService(
            fixture.Db,
            new StockService(fixture.Db, fixture.CurrentUser),
            fixture.CurrentUser,
            fixture.StockEvents,
            idempotencyService: fixture.Idempotency,
            stockAvailability: fixture.Availability);
        var request = new CheckoutRequest(
            checkoutId, fixture.OutletId, fixture.SessionId, TransactionChannel.Pos,
            20_000, 0, 0, 20_000,
            [new CheckoutItemRequest(fixture.ProductId, 2, 10_000, 0)],
            [new PaymentRequest(PaymentMethod.Cash, 20_000, null)]);

        var result = await transactions.CheckoutAsync(request);
        var replay = await transactions.CheckoutAsync(request);

        result.Status.Should().Be(TransactionStatus.Completed);
        replay.Id.Should().Be(result.Id);
        (await fixture.Db.StockReservations.SingleAsync()).Status.Should().Be(StockReservationStatus.Consumed);
        (await fixture.Db.InventoryStocks.SingleAsync()).QtyOnHand.Should().Be(3);
        (await fixture.Availability.GetReservedQuantityAsync(
            fixture.OutletId, fixture.ProductId, null)).Should().Be(0);
        fixture.Db.StockLedgers.Should().ContainSingle(x => x.MovementType == StockMovementType.Sale);
        var inventory = await new InventoryService(fixture.Db, fixture.Availability)
            .GetByStockLocationAsync(fixture.OutletId);
        inventory.Single().QtySold.Should().Be(2);
    }

    [Fact]
    public async Task ExpiredSerialHold_Should_BeReclaimable_AndNotFreedByStaleRelease()
    {
        var fixture = await Fixture.CreateAsync();
        var categoryId = await fixture.Db.Categories.Select(x => x.Id).SingleAsync();
        var productId = Guid.NewGuid();
        var serialId = Guid.NewGuid();
        fixture.Db.Products.Add(new Product
        {
            Id = productId, CategoryId = categoryId, Sku = "SER-1", Name = "Serial Product",
            BasePrice = 10_000, CostPrice = 7_000, Unit = "pcs", IsActive = true,
            TrackingMode = ProductTrackingModes.Serial
        });
        fixture.Db.InventoryStocks.Add(new InventoryStock
        {
            Id = Guid.NewGuid(), StockLocationId = fixture.OutletId, ProductId = productId, QtyOnHand = 1
        });
        fixture.Db.ProductSerials.Add(new ProductSerial
        {
            Id = serialId, ProductId = productId, StockLocationId = fixture.OutletId,
            SerialNumber = "SN-1", NormalizedSerialNumber = "SN-1",
            Status = ProductSerialStatuses.Available
        });
        await fixture.Db.SaveChangesAsync();

        var staleId = Guid.NewGuid();
        await fixture.Reservations.CreateAsync(new CreateStockReservationRequest(
            staleId, fixture.OutletId, fixture.SessionId,
            [new StockReservationItemRequest(productId, 1) { SerialIds = [serialId] }]));
        var stale = await fixture.Db.StockReservations.SingleAsync(x => x.Id == staleId);
        stale.ExpiresAt = DateTime.UtcNow.AddMinutes(-1);
        await fixture.Db.SaveChangesAsync();

        var liveId = Guid.NewGuid();
        await fixture.Reservations.CreateAsync(new CreateStockReservationRequest(
            liveId, fixture.OutletId, fixture.SessionId,
            [new StockReservationItemRequest(productId, 1) { SerialIds = [serialId] }]));

        await fixture.Reservations.ReleaseAsync(staleId, "kedaluwarsa");
        (await fixture.Db.ProductSerials.SingleAsync(x => x.Id == serialId)).Status
            .Should().Be(ProductSerialStatuses.Reserved);

        await fixture.Reservations.ReleaseAsync(liveId, "batal");
        (await fixture.Db.ProductSerials.SingleAsync(x => x.Id == serialId)).Status
            .Should().Be(ProductSerialStatuses.Available);
    }

    private sealed class Fixture
    {
        public AppDbContext Db { get; init; } = default!;
        public ICurrentUserService CurrentUser { get; init; } = default!;
        public IStockEventPublisher StockEvents { get; init; } = default!;
        public IIdempotencyService Idempotency { get; init; } = default!;
        public IStockAvailabilityService Availability { get; init; } = default!;
        public IStockReservationService Reservations { get; init; } = default!;
        public Guid OutletId { get; init; }
        public Guid SessionId { get; init; }
        public Guid ProductId { get; init; }

        public static async Task<Fixture> CreateAsync()
        {
            var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options, TestTenant.CurrentUser);
            var outletId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            var productId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            db.Outlets.Add(new Outlet { Id = outletId, Code = "OUT", Name = "Outlet", IsActive = true });
            db.StockLocations.Add(new StockLocation
            {
                Id = outletId, Type = StockLocationTypes.Outlet, Code = "OUT", Name = "Outlet",
                OutletId = outletId, IsActive = true
            });
            db.Users.Add(new User
            {
                Id = userId, Name = "Kasir", Email = "reservation@test.local", PasswordHash = "hash",
                RoleId = Guid.NewGuid(), OutletId = outletId
            });
            db.Categories.Add(new Category { Id = categoryId, Name = "Produk" });
            db.Products.Add(new Product
            {
                Id = productId, CategoryId = categoryId, Sku = "RES-1", Name = "Produk Reservasi",
                BasePrice = 10_000, CostPrice = 7_000, Unit = "pcs", IsActive = true,
                TrackingMode = ProductTrackingModes.Quantity
            });
            db.InventoryStocks.Add(new InventoryStock
            {
                Id = Guid.NewGuid(), StockLocationId = outletId, ProductId = productId, QtyOnHand = 5
            });
            db.CashierSessions.Add(new CashierSession
            {
                Id = sessionId, UserId = userId, OutletId = outletId, StockLocationId = outletId,
                OpeningCash = 0, Status = CashierSessionStatus.Open
            });
            await db.SaveChangesAsync();

            var currentUser = Substitute.For<ICurrentUserService>();
            currentUser.UserId.Returns(userId);
            currentUser.OutletId.Returns(outletId);
            currentUser.BusinessId.Returns(TestTenant.BusinessId);
            currentUser.Role.Returns("Kasir");
            var events = Substitute.For<IStockEventPublisher>();
            var idempotency = new IdempotencyService(db, currentUser);
            var availability = new StockAvailabilityService(db);
            return new Fixture
            {
                Db = db,
                CurrentUser = currentUser,
                StockEvents = events,
                Idempotency = idempotency,
                Availability = availability,
                Reservations = new StockReservationService(db, currentUser, idempotency, availability, events),
                OutletId = outletId,
                SessionId = sessionId,
                ProductId = productId
            };
        }
    }
}
