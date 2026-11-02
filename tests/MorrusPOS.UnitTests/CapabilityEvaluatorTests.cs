using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;

namespace MorrusPOS.UnitTests;

public class CapabilityEvaluatorTests
{
    [Fact]
    public async Task EffectiveCapability_Should_ApplyProfileBusinessAndOutletPrecedence()
    {
        var dbName = Guid.NewGuid().ToString();
        var businessId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var capabilityId = Guid.NewGuid();
        var outletId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await using (var system = new SystemDbContext(new DbContextOptionsBuilder<SystemDbContext>()
            .UseInMemoryDatabase(dbName).Options))
        {
            system.IndustryProfiles.Add(new IndustryProfile { Id = profileId, Code = "test", Name = "Test" });
            system.Capabilities.Add(new Capability
            {
                Id = capabilityId,
                Code = CapabilityCodes.InventoryTransfer,
                Name = "Transfer",
                Module = "Inventory",
                Scope = CapabilityScopes.Outlet,
                DefaultEnabled = false,
            });
            system.IndustryProfileCapabilities.Add(new IndustryProfileCapability
            {
                IndustryProfileId = profileId,
                CapabilityId = capabilityId,
                IsEnabled = true,
            });
            system.Businesses.Add(new Business
            {
                Id = businessId,
                Name = "Test",
                Slug = "test",
                Category = "Test",
                IndustryProfileId = profileId,
            });
            system.Outlets.Add(new Outlet { Id = outletId, BusinessId = businessId, Code = "T01", Name = "Test" });
            system.OutletCapabilityOverrides.Add(new OutletCapabilityOverride
            {
                BusinessId = businessId,
                OutletId = outletId,
                CapabilityId = capabilityId,
                IsEnabled = false,
                UpdatedByUserId = userId,
            });
            await system.SaveChangesAsync();
        }

        var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options;
        await using var db = new AppDbContext(options, new TenantUser(businessId));
        var evaluator = new CapabilityEvaluator(db, new TenantUser(businessId));

        (await evaluator.IsEnabledAsync(CapabilityCodes.InventoryTransfer, null)).Should().BeTrue();
        (await evaluator.IsEnabledAsync(CapabilityCodes.InventoryTransfer, outletId)).Should().BeFalse();

        await using (var systemUpdate = new SystemDbContext(new DbContextOptionsBuilder<SystemDbContext>()
            .UseInMemoryDatabase(dbName).Options))
        {
            systemUpdate.BusinessCapabilityOverrides.Add(new BusinessCapabilityOverride
            {
                BusinessId = businessId,
                CapabilityId = capabilityId,
                IsEnabled = false,
                UpdatedByUserId = userId,
            });
            var outletOverride = await systemUpdate.OutletCapabilityOverrides.SingleAsync();
            outletOverride.IsEnabled = true;
            await systemUpdate.SaveChangesAsync();
        }
        db.ChangeTracker.Clear();
        evaluator.Invalidate();

        var result = await evaluator.GetEffectiveAsync(outletId);
        result.Capabilities.Single().Enabled.Should().BeFalse();
        result.Capabilities.Single().Source.Should().Be("BusinessDisabled");
    }

    [Fact]
    public async Task UnknownCapability_Should_FailClosed()
    {
        var businessId = Guid.NewGuid();
        var profileId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        await using (var system = new SystemDbContext(new DbContextOptionsBuilder<SystemDbContext>()
            .UseInMemoryDatabase(dbName).Options))
        {
            system.IndustryProfiles.Add(new IndustryProfile { Id = profileId, Code = "test", Name = "Test" });
            system.Businesses.Add(new Business
            {
                Id = businessId, Name = "Test", Slug = "test", Category = "Test", IndustryProfileId = profileId,
            });
            await system.SaveChangesAsync();
        }

        var user = new TenantUser(businessId);
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(dbName).Options, user);
        var evaluator = new CapabilityEvaluator(db, user);
        (await evaluator.IsEnabledAsync("does.not.exist", null)).Should().BeFalse();
    }

    private sealed class TenantUser(Guid businessId) : ICurrentUserService
    {
        public Guid? UserId => Guid.NewGuid();
        public Guid? OutletId => null;
        public Guid? BusinessId => businessId;
        public string? Role => "Owner";
        public bool IsAuthenticated => true;
    }
}
