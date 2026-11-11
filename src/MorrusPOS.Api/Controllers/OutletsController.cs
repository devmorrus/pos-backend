using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Owner,Admin,Keuangan")]
public class OutletsController : ControllerBase
{
    private readonly AppDbContext _dbContext;

    public OutletsController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OutletDto>>> GetAll(CancellationToken ct)
    {
        var outlets = await _dbContext.Outlets
            .AsNoTracking()
            .OrderBy(o => o.Name)
            .Select(o => new OutletDto(
                o.Id,
                o.Code,
                o.Name,
                o.Address,
                o.Phone,
                o.IsActive,
                o.CreatedAt,
                o.UpdatedAt))
            .ToListAsync(ct);

        return Ok(outlets);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OutletDto>> GetById(Guid id, CancellationToken ct)
    {
        var outlet = await _dbContext.Outlets
            .AsNoTracking()
            .Where(o => o.Id == id)
            .Select(o => new OutletDto(
                o.Id,
                o.Code,
                o.Name,
                o.Address,
                o.Phone,
                o.IsActive,
                o.CreatedAt,
                o.UpdatedAt))
            .FirstOrDefaultAsync(ct);

        if (outlet is null)
        {
            return NotFound("Cabang tidak ditemukan.");
        }

        return Ok(outlet);
    }

    [HttpPost]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<OutletDto>> Create([FromBody] CreateOutletRequest request, CancellationToken ct)
    {
        var normalized = NormalizeRequest(request);
        var validationError = ValidateRequest(normalized);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var codeExists = await _dbContext.Outlets
            .AnyAsync(o => o.Code.ToLower() == normalized.Code.ToLower(), ct);
        if (codeExists)
        {
            return Conflict("Kode cabang sudah digunakan.");
        }

        var locationCodeExists = await _dbContext.StockLocations
            .AnyAsync(l => l.Code.ToLower() == normalized.Code.ToLower(), ct);
        if (locationCodeExists)
        {
            return Conflict("Kode cabang sudah digunakan oleh lokasi stok lain.");
        }

        var outlet = new Outlet
        {
            Id = Guid.NewGuid(),
            Code = normalized.Code,
            Name = normalized.Name,
            Address = normalized.Address,
            Phone = normalized.Phone,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _dbContext.Outlets.Add(outlet);
        _dbContext.StockLocations.Add(new StockLocation
        {
            Id = outlet.Id,
            Type = StockLocationTypes.Outlet,
            Code = outlet.Code,
            Name = outlet.Name,
            OutletId = outlet.Id,
            Address = outlet.Address,
            Phone = outlet.Phone,
            IsActive = true,
            CreatedAt = outlet.CreatedAt,
            UpdatedAt = outlet.UpdatedAt,
        });

        var products = await _dbContext.Products
            .AsNoTracking()
            .Where(p => p.IsActive && p.TrackingMode != ProductTrackingModes.None)
            .Select(p => new { p.Id, Variants = p.Variants.Where(v => v.IsActive).Select(v => (Guid?)v.Id).ToList() })
            .ToListAsync(ct);

        if (products.Count > 0)
        {
            var now = DateTime.UtcNow;
            var inventorySeeds = products
                .SelectMany(p => new[] { (Guid?)null }.Concat(p.Variants)
                    .Select(variantId => new InventoryStock
                    {
                        Id = Guid.NewGuid(),
                        ProductId = p.Id,
                        ProductVariantId = variantId,
                        StockLocationId = outlet.Id,
                        QtyOnHand = 0,
                        MinStockAlert = 0,
                        UpdatedAt = now
                    }));

            _dbContext.InventoryStocks.AddRange(inventorySeeds);
        }

        await _dbContext.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = outlet.Id }, MapToDto(outlet));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner")]
    public async Task<ActionResult<OutletDto>> Update(Guid id, [FromBody] UpdateOutletRequest request, CancellationToken ct)
    {
        var outlet = await _dbContext.Outlets.FirstOrDefaultAsync(o => o.Id == id, ct);
        if (outlet is null)
        {
            return NotFound("Cabang tidak ditemukan.");
        }

        var normalized = NormalizeRequest(request);
        var validationError = ValidateRequest(normalized);
        if (validationError is not null)
        {
            return BadRequest(validationError);
        }

        var codeExists = await _dbContext.Outlets
            .AnyAsync(o => o.Id != id && o.Code.ToLower() == normalized.Code.ToLower(), ct);
        if (codeExists)
        {
            return Conflict("Kode cabang sudah digunakan.");
        }

        var locationCodeExists = await _dbContext.StockLocations
            .AnyAsync(l => l.Id != id && l.Code.ToLower() == normalized.Code.ToLower(), ct);
        if (locationCodeExists)
        {
            return Conflict("Kode cabang sudah digunakan oleh lokasi stok lain.");
        }

        if (!normalized.IsActive && outlet.IsActive)
        {
            var hasActiveUsers = await _dbContext.Users
                .AnyAsync(u => u.OutletId == id && u.IsActive, ct);

            if (hasActiveUsers)
            {
                return Conflict("Cabang tidak dapat dinonaktifkan karena masih memiliki pengguna aktif.");
            }
            var hasTransferInTransit = await _dbContext.StockTransfers.AnyAsync(t =>
                (t.FromStockLocationId == id || t.ToStockLocationId == id) &&
                (t.Status == StockTransferStatus.InTransit || t.Status == StockTransferStatus.Pending), ct);
            if (hasTransferInTransit)
            {
                return Conflict("Cabang tidak dapat dinonaktifkan karena masih memiliki transfer dalam perjalanan.");
            }
        }

        outlet.Code = normalized.Code;
        outlet.Name = normalized.Name;
        outlet.Address = normalized.Address;
        outlet.Phone = normalized.Phone;
        outlet.IsActive = normalized.IsActive;
        outlet.UpdatedAt = DateTime.UtcNow;

        var stockLocation = await _dbContext.StockLocations
            .FirstOrDefaultAsync(l => l.Id == id, ct);
        if (stockLocation is null)
        {
            stockLocation = new StockLocation
            {
                Id = outlet.Id,
                Type = StockLocationTypes.Outlet,
                OutletId = outlet.Id,
                CreatedAt = outlet.CreatedAt,
            };
            _dbContext.StockLocations.Add(stockLocation);
        }

        stockLocation.Code = outlet.Code;
        stockLocation.Name = outlet.Name;
        stockLocation.Address = outlet.Address;
        stockLocation.Phone = outlet.Phone;
        stockLocation.IsActive = outlet.IsActive;
        stockLocation.UpdatedAt = outlet.UpdatedAt;

        await _dbContext.SaveChangesAsync(ct);

        return Ok(MapToDto(outlet));
    }

    private static OutletDto MapToDto(Outlet outlet)
        => new(
            outlet.Id,
            outlet.Code,
            outlet.Name,
            outlet.Address,
            outlet.Phone,
            outlet.IsActive,
            outlet.CreatedAt,
            outlet.UpdatedAt);

    private static OutletMutationRequest NormalizeRequest(CreateOutletRequest request)
        => new(
            request.Code.Trim().ToUpperInvariant(),
            request.Name.Trim(),
            string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            true);

    private static OutletMutationRequest NormalizeRequest(UpdateOutletRequest request)
        => new(
            request.Code.Trim().ToUpperInvariant(),
            request.Name.Trim(),
            string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            request.IsActive);

    private static string? ValidateRequest(OutletMutationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return "Kode cabang wajib diisi.";
        }

        var trimmedCode = request.Code.Trim();
        if (trimmedCode.Length < 3 || trimmedCode.Length > 20)
        {
            return "Kode cabang harus berkisar antara 3 sampai 20 karakter.";
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(trimmedCode, "^[a-zA-Z0-9\\-_]+$"))
        {
            return "Kode cabang hanya boleh berisi huruf, angka, strip (-), dan underscore (_).";
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return "Nama cabang wajib diisi.";
        }

        var trimmedName = request.Name.Trim();
        if (trimmedName.Length < 3 || trimmedName.Length > 150)
        {
            return "Nama cabang harus berkisar antara 3 sampai 150 karakter.";
        }

        if (request.Phone is not null && request.Phone.Trim().Length > 0)
        {
            var trimmedPhone = request.Phone.Trim();
            if (!System.Text.RegularExpressions.Regex.IsMatch(trimmedPhone, "^[0-9+\\-\\s()]+$"))
            {
                return "Nomor telepon tidak valid.";
            }
            if (trimmedPhone.Length > 20)
            {
                return "Nomor telepon maksimal 20 karakter.";
            }
        }

        return null;
    }
}

public record OutletDto(
    Guid Id,
    string Code,
    string Name,
    string? Address,
    string? Phone,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateOutletRequest(
    string Code,
    string Name,
    string? Address,
    string? Phone);

public record UpdateOutletRequest(
    string Code,
    string Name,
    string? Address,
    string? Phone,
    bool IsActive) : OutletMutationRequest(Code, Name, Address, Phone, IsActive);

public record OutletMutationRequest(
    string Code,
    string Name,
    string? Address,
    string? Phone,
    bool IsActive = true);
