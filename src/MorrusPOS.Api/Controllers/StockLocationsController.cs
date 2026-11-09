using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/stock-locations")]
[Authorize(Roles = "Owner,Admin,Keuangan,Gudang,KepalaCabang")]
public class StockLocationsController : ControllerBase
{
    private static readonly string[] ManageRoles = ["Owner", "Admin"];

    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public StockLocationsController(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<StockLocationDto>>> GetAll(
        [FromQuery] string? type,
        [FromQuery] bool includeInactive = true,
        CancellationToken ct = default)
    {
        var query = _db.StockLocations.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!StockLocationTypes.All.Contains(type))
                return BadRequest("Tipe lokasi stok tidak valid.");
            query = query.Where(x => x.Type == type);
        }

        if (!includeInactive)
            query = query.Where(x => x.IsActive);

        if (!ManageRoles.Contains(_currentUser.Role ?? string.Empty) && _currentUser.Role != "Keuangan")
        {
            var userId = _currentUser.UserId ?? Guid.Empty;
            var outletId = _currentUser.OutletId;
            query = query.Where(x =>
                (outletId != null && x.OutletId == outletId)
                || _db.UserStockLocationAccesses.Any(a => a.StockLocationId == x.Id && a.UserId == userId));
        }

        var locations = await query
            .OrderBy(x => x.Type)
            .ThenBy(x => x.Name)
            .Select(x => new StockLocationDto(
                x.Id,
                x.Type,
                x.Code,
                x.Name,
                x.OutletId,
                x.Outlet != null ? x.Outlet.Name : null,
                x.Address,
                x.Phone,
                x.IsActive,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(ct);

        return Ok(locations);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<StockLocationDto>> GetById(Guid id, CancellationToken ct)
    {
        var location = await _db.StockLocations
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new StockLocationDto(
                x.Id,
                x.Type,
                x.Code,
                x.Name,
                x.OutletId,
                x.Outlet != null ? x.Outlet.Name : null,
                x.Address,
                x.Phone,
                x.IsActive,
                x.CreatedAt,
                x.UpdatedAt))
            .FirstOrDefaultAsync(ct);

        if (location is null)
            return NotFound("Lokasi stok tidak ditemukan.");

        if (!ManageRoles.Contains(_currentUser.Role ?? string.Empty) && _currentUser.Role != "Keuangan")
        {
            var userId = _currentUser.UserId ?? Guid.Empty;
            var hasAccess = location.OutletId == _currentUser.OutletId
                || await _db.UserStockLocationAccesses.AsNoTracking()
                    .AnyAsync(x => x.UserId == userId && x.StockLocationId == id, ct);
            if (!hasAccess) return Forbid();
        }

        return Ok(location);
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult<StockLocationDto>> Create(
        [FromBody] CreateWarehouseRequest request,
        CancellationToken ct)
    {
        var error = Validate(request.Code, request.Name, request.Phone);
        if (error is not null)
            return BadRequest(error);

        var code = request.Code.Trim().ToUpperInvariant();
        if (await _db.StockLocations.AnyAsync(x => x.Code.ToLower() == code.ToLower(), ct))
            return Conflict("Kode lokasi stok sudah digunakan.");

        var location = new StockLocation
        {
            Id = Guid.NewGuid(),
            Type = StockLocationTypes.Warehouse,
            Code = code,
            Name = request.Name.Trim(),
            Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim(),
            Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.StockLocations.Add(location);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = location.Id }, MapToDto(location));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult<StockLocationDto>> Update(
        Guid id,
        [FromBody] UpdateStockLocationRequest request,
        CancellationToken ct)
    {
        var location = await _db.StockLocations.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (location is null)
            return NotFound("Lokasi stok tidak ditemukan.");
        if (location.Type != StockLocationTypes.Warehouse)
            return Conflict("Lokasi cabang dikelola melalui menu Cabang.");

        var error = Validate(request.Code, request.Name, request.Phone);
        if (error is not null)
            return BadRequest(error);

        var code = request.Code.Trim().ToUpperInvariant();
        if (await _db.StockLocations.AnyAsync(x => x.Id != id && x.Code.ToLower() == code.ToLower(), ct))
            return Conflict("Kode lokasi stok sudah digunakan.");

        if (!request.IsActive && location.IsActive)
        {
            var hasStock = await _db.InventoryStocks
                .AnyAsync(x => x.StockLocationId == id && x.QtyOnHand != 0, ct);
            if (hasStock)
                return Conflict("Gudang tidak dapat dinonaktifkan karena masih memiliki stok.");
        }

        location.Code = code;
        location.Name = request.Name.Trim();
        location.Address = string.IsNullOrWhiteSpace(request.Address) ? null : request.Address.Trim();
        location.Phone = string.IsNullOrWhiteSpace(request.Phone) ? null : request.Phone.Trim();
        location.IsActive = request.IsActive;
        location.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(MapToDto(location));
    }

    [HttpGet("{id:guid}/users")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult<IReadOnlyList<StockLocationUserDto>>> GetUsers(Guid id, CancellationToken ct)
    {
        if (!await _db.StockLocations.AnyAsync(x => x.Id == id, ct))
            return NotFound("Lokasi stok tidak ditemukan.");

        var users = await _db.UserStockLocationAccesses
            .AsNoTracking()
            .Where(x => x.StockLocationId == id)
            .OrderByDescending(x => x.IsDefault)
            .ThenBy(x => x.User.Name)
            .Select(x => new StockLocationUserDto(x.UserId, x.User.Name, x.User.Email, x.IsDefault))
            .ToListAsync(ct);

        return Ok(users);
    }

    [HttpPut("{id:guid}/users")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult<IReadOnlyList<StockLocationUserDto>>> ReplaceUsers(
        Guid id,
        [FromBody] ReplaceStockLocationUsersRequest request,
        CancellationToken ct)
    {
        if (!await _db.StockLocations.AnyAsync(x => x.Id == id, ct))
            return NotFound("Lokasi stok tidak ditemukan.");

        var userIds = request.UserIds.Distinct().ToArray();
        var validUserIds = await _db.Users
            .Where(x => userIds.Contains(x.Id) && x.IsActive)
            .Select(x => x.Id)
            .ToListAsync(ct);
        if (validUserIds.Count != userIds.Length)
            return BadRequest("Satu atau lebih user tidak valid atau tidak aktif.");

        var existing = await _db.UserStockLocationAccesses
            .Where(x => x.StockLocationId == id)
            .ToListAsync(ct);

        _db.UserStockLocationAccesses.RemoveRange(
            existing.Where(x => !validUserIds.Contains(x.UserId)));

        var newUserIds = validUserIds.Except(existing.Select(x => x.UserId)).ToArray();
        if (newUserIds.Length > 0)
        {
            var usersWithDefault = await _db.UserStockLocationAccesses
                .Where(x => newUserIds.Contains(x.UserId) && x.IsDefault)
                .Select(x => x.UserId)
                .ToListAsync(ct);

            foreach (var userId in newUserIds)
            {
                _db.UserStockLocationAccesses.Add(new UserStockLocationAccess
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    StockLocationId = id,
                    IsDefault = !usersWithDefault.Contains(userId)
                        && !existing.Any(x => x.UserId == userId && x.IsDefault),
                    CreatedBy = _currentUser.UserId,
                    CreatedAt = DateTime.UtcNow,
                });
            }
        }

        await _db.SaveChangesAsync(ct);

        return await GetUsers(id, ct);
    }

    private static StockLocationDto MapToDto(StockLocation location)
        => new(
            location.Id,
            location.Type,
            location.Code,
            location.Name,
            location.OutletId,
            location.Outlet?.Name,
            location.Address,
            location.Phone,
            location.IsActive,
            location.CreatedAt,
            location.UpdatedAt);

    private static string? Validate(string code, string name, string? phone)
    {
        var trimmedCode = code?.Trim() ?? string.Empty;
        if (trimmedCode.Length < 2 || trimmedCode.Length > 30)
            return "Kode lokasi stok harus berkisar antara 2 sampai 30 karakter.";
        if (!System.Text.RegularExpressions.Regex.IsMatch(trimmedCode, "^[a-zA-Z0-9\\-_]+$"))
            return "Kode lokasi stok hanya boleh berisi huruf, angka, strip (-), dan underscore (_).";

        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length < 3 || trimmedName.Length > 150)
            return "Nama lokasi stok harus berkisar antara 3 sampai 150 karakter.";

        if (!string.IsNullOrWhiteSpace(phone) && phone.Trim().Length > 30)
            return "Nomor telepon maksimal 30 karakter.";

        return null;
    }
}

public record StockLocationDto(
    Guid Id,
    string Type,
    string Code,
    string Name,
    Guid? OutletId,
    string? OutletName,
    string? Address,
    string? Phone,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreateWarehouseRequest(
    string Code,
    string Name,
    string? Address,
    string? Phone);

public record UpdateStockLocationRequest(
    string Code,
    string Name,
    string? Address,
    string? Phone,
    bool IsActive);

public record StockLocationUserDto(
    Guid UserId,
    string UserName,
    string Email,
    bool IsDefault);

public record ReplaceStockLocationUsersRequest(IReadOnlyList<Guid> UserIds);
