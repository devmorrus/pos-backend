using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Api.Controllers;

/// <summary>
/// Public endpoints untuk customer storefront — tidak memerlukan autentikasi.
/// Hanya mengekspos data yang aman untuk publik (nama, harga jual, stok tersedia).
/// </summary>
[ApiController]
[Route("api/public")]
[AllowAnonymous]
public class PublicController : ControllerBase
{
    private readonly AppDbContext _db;

    public PublicController(AppDbContext db)
    {
        _db = db;
    }

    // -------------------------------------------------------------------------
    // GET /api/public/outlets
    // Daftar outlet yang aktif — ditampilkan di halaman pemilihan outlet customer
    // -------------------------------------------------------------------------
    [HttpGet("outlets")]
    public async Task<ActionResult<IReadOnlyList<PublicOutletDto>>> GetOutlets(CancellationToken ct)
    {
        var outlets = await _db.Outlets
            .AsNoTracking()
            .Where(o => o.IsActive)
            .OrderBy(o => o.Name)
            .Select(o => new PublicOutletDto(
                o.Id,
                o.Code,
                o.Name,
                o.Address,
                o.Phone))
            .ToListAsync(ct);

        return Ok(outlets);
    }

    // -------------------------------------------------------------------------
    // GET /api/public/outlets/{code}
    // Detail satu outlet by Code (slug di URL).
    // Dipakai saat customer reload page atau direct URL — untuk restore context.
    // -------------------------------------------------------------------------
    [HttpGet("outlets/{code}")]
    public async Task<ActionResult<PublicOutletDto>> GetOutletByCode(string code, CancellationToken ct)
    {
        var outlet = await _db.Outlets
            .AsNoTracking()
            .Where(o => o.Code.ToLower() == code.ToLower() && o.IsActive)
            .Select(o => new PublicOutletDto(
                o.Id,
                o.Code,
                o.Name,
                o.Address,
                o.Phone))
            .FirstOrDefaultAsync(ct);

        if (outlet is null)
            return NotFound($"Outlet dengan kode '{code}' tidak ditemukan atau tidak aktif.");

        return Ok(outlet);
    }

    // -------------------------------------------------------------------------
    // GET /api/public/catalog/{outletCode}
    // Katalog produk aktif + stok tersedia untuk outlet tertentu.
    // Join antara Products, InventoryStocks, dan Categories.
    // Hanya ekspos harga jual (Price), bukan HPP/CostPrice.
    // -------------------------------------------------------------------------
    [HttpGet("catalog/{outletCode}")]
    public async Task<ActionResult<IReadOnlyList<PublicProductDto>>> GetCatalog(
        string outletCode,
        CancellationToken ct)
    {
        // Validasi outlet dulu
        var outletExists = await _db.Outlets
            .AsNoTracking()
            .AnyAsync(o => o.Code.ToLower() == outletCode.ToLower() && o.IsActive, ct);

        if (!outletExists)
            return NotFound($"Outlet dengan kode '{outletCode}' tidak ditemukan atau tidak aktif.");

        var rawProducts = await _db.InventoryStocks
            .AsNoTracking()
            .Where(s =>
                s.Outlet.Code.ToLower() == outletCode.ToLower() &&
                s.Product.IsActive)
            .Select(s => new PublicProductDto(
                s.Product.Id,
                s.Product.Name,
                s.Product.Sku,
                s.Product.BasePrice,
                s.Product.Unit,
                s.Product.Category != null ? s.Product.Category.Name : null,
                s.Product.Category != null ? s.Product.Category.Id : (Guid?)null,
                s.QtyOnHand))
            .ToListAsync(ct);

        var products = rawProducts
            .OrderBy(p => p.CategoryName ?? "~")
            .ThenBy(p => p.Name)
            .ToList();

        return Ok(products);
    }

    // -------------------------------------------------------------------------
    // GET /api/public/categories
    // Semua kategori — untuk filter di halaman katalog customer
    // -------------------------------------------------------------------------
    [HttpGet("categories")]
    public async Task<ActionResult<IReadOnlyList<PublicCategoryDto>>> GetCategories(CancellationToken ct)
    {
        var categories = await _db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new PublicCategoryDto(c.Id, c.Name))
            .ToListAsync(ct);

        return Ok(categories);
    }
}

// ---------------------------------------------------------------------------
// DTOs — hanya ekspos data publik yang aman
// ---------------------------------------------------------------------------

public record PublicOutletDto(
    Guid Id,
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
    decimal QtyOnHand);

public record PublicCategoryDto(
    Guid Id,
    string Name);
