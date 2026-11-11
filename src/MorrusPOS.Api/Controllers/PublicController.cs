using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Capabilities;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Api.Controllers;

/// <summary>
/// Public storefront. The directory endpoint exposes only safe listing fields;
/// every catalog/data endpoint first resolves a business slug, then executes
/// through the normal tenant-filtered/RLS AppDbContext.
/// </summary>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
public class PublicController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly SystemDbContext _systemDb;
    private readonly ITenantContextSetter _tenantContext;
    private readonly ICapabilityEvaluator _capabilities;
    private readonly IStockAvailabilityService _availability;

    public PublicController(
        AppDbContext db,
        SystemDbContext systemDb,
        ITenantContextSetter tenantContext,
        ICapabilityEvaluator capabilities,
        IStockAvailabilityService availability)
    {
        _db = db;
        _systemDb = systemDb;
        _tenantContext = tenantContext;
        _capabilities = capabilities;
        _availability = availability;
    }

    [HttpGet("industry-profiles")]
    public async Task<ActionResult<IReadOnlyList<IndustryProfileDto>>> GetIndustryProfiles(CancellationToken ct)
    {
        var profiles = await _systemDb.IndustryProfiles
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.SortOrder)
            .Select(x => new IndustryProfileDto(x.Id, x.Code, x.Name, x.Description))
            .ToListAsync(ct);
        return Ok(profiles);
    }

    // Explicit public directory used to select a tenant storefront.
    [HttpGet("outlets")]
    public async Task<ActionResult<IReadOnlyList<PublicOutletDto>>> GetDirectory(CancellationToken ct)
    {
        var storefront = await _systemDb.Capabilities.AsNoTracking()
            .SingleAsync(x => x.Code == CapabilityCodes.StorefrontPublic && x.IsActive, ct);
        var rows = await _systemDb.Outlets
            .AsNoTracking()
            .Where(o => o.IsActive)
            .OrderBy(o => o.Business.Name)
            .ThenBy(o => o.Name)
            .Select(o => new
            {
                Outlet = new PublicOutletDto(o.Id, o.Business.Slug, o.Code, o.Name, o.Address, o.Phone),
                o.BusinessId,
                o.Business.IndustryProfileId,
            })
            .ToListAsync(ct);

        var profileDefaults = await _systemDb.IndustryProfileCapabilities.AsNoTracking()
            .Where(x => x.CapabilityId == storefront.Id)
            .ToDictionaryAsync(x => x.IndustryProfileId, x => x.IsEnabled, ct);
        var businessOverrides = await _systemDb.BusinessCapabilityOverrides.AsNoTracking()
            .Where(x => x.CapabilityId == storefront.Id)
            .ToDictionaryAsync(x => x.BusinessId, x => x.IsEnabled, ct);
        var outletOverrides = await _systemDb.OutletCapabilityOverrides.AsNoTracking()
            .Where(x => x.CapabilityId == storefront.Id)
            .ToDictionaryAsync(x => x.OutletId, x => x.IsEnabled, ct);

        var outlets = rows
            .Where(row =>
            {
                var businessEnabled = businessOverrides.TryGetValue(row.BusinessId, out var businessOverride)
                    ? businessOverride
                    : profileDefaults.GetValueOrDefault(row.IndustryProfileId, storefront.DefaultEnabled);
                return businessEnabled && outletOverrides.GetValueOrDefault(row.Outlet.Id, true);
            })
            .Select(row => row.Outlet)
            .ToList();

        return Ok(outlets);
    }

    [HttpGet("{businessSlug}/outlets")]
    public async Task<ActionResult<IReadOnlyList<PublicOutletDto>>> GetOutlets(
        string businessSlug,
        CancellationToken ct)
    {
        var businessId = await ResolveTenantAsync(businessSlug, ct);
        if (!businessId.HasValue)
            return NotFound($"Business '{businessSlug}' tidak ditemukan.");

        var outlets = await _db.Outlets
            .AsNoTracking()
            .Where(o => o.IsActive)
            .OrderBy(o => o.Name)
            .Select(o => new PublicOutletDto(
                o.Id, businessSlug, o.Code, o.Name, o.Address, o.Phone))
            .ToListAsync(ct);

        var visible = new List<PublicOutletDto>();
        foreach (var outlet in outlets)
        {
            if (await _capabilities.IsEnabledAsync(CapabilityCodes.StorefrontPublic, outlet.Id, ct))
                visible.Add(outlet);
        }

        return Ok(visible);
    }

    [HttpGet("{businessSlug}/outlets/{code}")]
    public async Task<ActionResult<PublicOutletDto>> GetOutletByCode(
        string businessSlug,
        string code,
        CancellationToken ct)
    {
        var businessId = await ResolveTenantAsync(businessSlug, ct);
        if (!businessId.HasValue)
            return NotFound($"Business '{businessSlug}' tidak ditemukan.");

        var outlet = await _db.Outlets
            .AsNoTracking()
            .Where(o => o.Code.ToLower() == code.ToLower() && o.IsActive)
            .Select(o => new PublicOutletDto(
                o.Id, businessSlug, o.Code, o.Name, o.Address, o.Phone))
            .FirstOrDefaultAsync(ct);

        if (outlet is not null &&
            !await _capabilities.IsEnabledAsync(CapabilityCodes.StorefrontPublic, outlet.Id, ct))
            outlet = null;

        return outlet is null
            ? NotFound($"Outlet '{code}' tidak ditemukan atau tidak aktif.")
            : Ok(outlet);
    }

    [HttpGet("{businessSlug}/catalog/{outletCode}")]
    public async Task<ActionResult<IReadOnlyList<PublicProductDto>>> GetCatalog(
        string businessSlug,
        string outletCode,
        CancellationToken ct)
    {
        var businessId = await ResolveTenantAsync(businessSlug, ct);
        if (!businessId.HasValue)
            return NotFound($"Business '{businessSlug}' tidak ditemukan.");

        var outletId = await _db.Outlets
            .AsNoTracking()
            .Where(o => o.Code.ToLower() == outletCode.ToLower() && o.IsActive)
            .Select(o => (Guid?)o.Id)
            .FirstOrDefaultAsync(ct);
        if (!outletId.HasValue)
            return NotFound($"Outlet '{outletCode}' tidak ditemukan atau tidak aktif.");
        if (!await _capabilities.IsEnabledAsync(CapabilityCodes.StorefrontPublic, outletId.Value, ct))
            return NotFound();

        var productRows = await _db.InventoryStocks
            .AsNoTracking()
            .Where(s => s.StockLocationId == outletId.Value && s.Product.IsActive &&
                        s.Product.TrackingMode != ProductTrackingModes.None)
            .OrderBy(s => s.Product.Category.Name)
            .ThenBy(s => s.Product.Name)
            .Select(s => new
            {
                s.Product.Id, s.Product.Name, s.Product.Sku, s.Product.BasePrice, s.Product.Unit,
                CategoryName = s.Product.Category.Name, CategoryId = s.Product.Category.Id,
                s.ProductVariantId, s.QtyOnHand
            })
            .ToListAsync(ct);
        var reserved = await _availability.GetReservedQuantitiesAsync(outletId.Value, ct);
        var products = productRows.Select(row =>
        {
            var qtyReserved = reserved.GetValueOrDefault(new StockAvailabilityKey(row.Id, row.ProductVariantId));
            var qtyAvailable = Math.Max(0, row.QtyOnHand - qtyReserved);
            return new PublicProductDto(row.Id, row.Name, row.Sku, row.BasePrice, row.Unit,
                row.CategoryName, row.CategoryId, qtyAvailable)
            {
                QtyPhysical = row.QtyOnHand,
                QtyReserved = qtyReserved,
                QtyAvailable = qtyAvailable
            };
        }).ToList();

        return Ok(products);
    }

    [HttpGet("{businessSlug}/categories")]
    public async Task<ActionResult<IReadOnlyList<PublicCategoryDto>>> GetCategories(
        string businessSlug,
        CancellationToken ct)
    {
        var businessId = await ResolveTenantAsync(businessSlug, ct);
        if (!businessId.HasValue)
            return NotFound($"Business '{businessSlug}' tidak ditemukan.");
        if (!await _capabilities.IsEnabledAsync(CapabilityCodes.StorefrontPublic, null, ct))
            return NotFound();

        var categories = await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new PublicCategoryDto(c.Id, c.Name))
            .ToListAsync(ct);

        return Ok(categories);
    }

    private async Task<Guid?> ResolveTenantAsync(string businessSlug, CancellationToken ct)
    {
        var businessId = await _systemDb.Businesses
            .AsNoTracking()
            .Where(b => b.Slug.ToLower() == businessSlug.ToLower())
            .Select(b => (Guid?)b.Id)
            .FirstOrDefaultAsync(ct);

        if (businessId.HasValue)
            _tenantContext.SetBusinessOverride(businessId.Value);

        return businessId;
    }
}

public record PublicOutletDto(
    Guid Id,
    string BusinessSlug,
    string Code,
    string Name,
    string? Address,
    string? Phone);

public record PublicProductDto(
    Guid Id,
    string Name,
    string Sku,
    decimal BasePrice,
    string Unit,
    string? CategoryName,
    Guid? CategoryId,
    decimal QtyOnHand)
{
    public decimal QtyPhysical { get; init; }
    public decimal QtyReserved { get; init; }
    public decimal QtyAvailable { get; init; }
}

public record PublicCategoryDto(Guid Id, string Name);
