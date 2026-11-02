using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.UnitTests;

public class TenantIsolationTests
{
    [Fact]
    public async Task QueryFilters_FailClosed_AndOnlyReturnCurrentBusiness()
    {
        var dbName = Guid.NewGuid().ToString();
        var businessA = Guid.NewGuid();
        var businessB = Guid.NewGuid();
        var systemOptions = new DbContextOptionsBuilder<SystemDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using (var system = new SystemDbContext(systemOptions))
        {
            system.Businesses.AddRange(
                BusinessFor(businessA, "a"),
                BusinessFor(businessB, "b"));
            system.Outlets.AddRange(
                OutletFor(businessA, "A"),
                OutletFor(businessB, "B"));
            await system.SaveChangesAsync();
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        await using var withoutTenant = new AppDbContext(options);
        (await withoutTenant.Outlets.CountAsync()).Should().Be(0);
        (await withoutTenant.Businesses.CountAsync()).Should().Be(0);

        await using var tenantA = new AppDbContext(options, new CurrentTenant(businessA));
        var visible = await tenantA.Outlets.SingleAsync();
        visible.BusinessId.Should().Be(businessA);
        (await tenantA.Businesses.SingleAsync()).Id.Should().Be(businessA);
    }

    [Fact]
    public async Task SaveGuard_RejectsCrossBusinessInsertAndBusinessIdMutation()
    {
        var businessA = Guid.NewGuid();
        var businessB = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options, new CurrentTenant(businessA));

        db.Outlets.Add(OutletFor(businessB, "foreign"));
        await db.Invoking(x => x.SaveChangesAsync())
            .Should().ThrowAsync<InvalidOperationException>();

        db.ChangeTracker.Clear();
        var outlet = OutletFor(businessA, "own");
        db.Outlets.Add(outlet);
        await db.SaveChangesAsync();

        outlet.BusinessId = businessB;
        await db.Invoking(x => x.SaveChangesAsync())
            .Should().ThrowAsync<InvalidOperationException>();
    }

    private static Outlet OutletFor(Guid businessId, string code) => new()
    {
        Id = Guid.NewGuid(),
        BusinessId = businessId,
        Code = code,
        Name = code,
        IsActive = true,
    };

    private static Business BusinessFor(Guid id, string slug) => new()
    {
        Id = id,
        Name = slug,
        Slug = slug,
        Category = "Test",
    };

    private sealed class CurrentTenant(Guid businessId) : ICurrentUserService
    {
        public Guid? UserId => Guid.NewGuid();
        public Guid? OutletId => null;
        public Guid? BusinessId => businessId;
        public string? Role => "Owner";
        public bool IsAuthenticated => true;
    }
}
