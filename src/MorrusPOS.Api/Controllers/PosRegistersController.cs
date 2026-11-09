using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/pos-registers")]
[Authorize(Roles = "Owner,Admin,Keuangan,Gudang,KepalaCabang,Kasir")]
public class PosRegistersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IConfiguration _configuration;

    public PosRegistersController(
        AppDbContext db,
        ICurrentUserService currentUser,
        IConfiguration configuration)
    {
        _db = db;
        _currentUser = currentUser;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PosRegisterDto>>> GetAll(
        [FromQuery] Guid? outletId,
        [FromQuery] bool includeInactive = true,
        CancellationToken ct = default)
    {
        var query = _db.PosRegisters.AsNoTracking();

        if (outletId.HasValue)
        {
            if (!CanAccessOutlet(outletId.Value))
                return Forbid();
            query = query.Where(x => x.OutletId == outletId.Value);
        }
        else if (!CanAccessAllOutlets())
        {
            var ownOutletId = _currentUser.OutletId;
            if (ownOutletId is null)
                return Ok(Array.Empty<PosRegisterDto>());
            query = query.Where(x => x.OutletId == ownOutletId);
        }

        if (!includeInactive)
            query = query.Where(x => x.IsActive);

        var registers = await query
            .OrderBy(x => x.Outlet.Name)
            .ThenBy(x => x.Name)
            .Select(x => new PosRegisterDto(
                x.Id,
                x.OutletId,
                x.Outlet.Name,
                x.StockLocationId,
                x.Code,
                x.Name,
                x.IsActive,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(ct);

        return Ok(registers);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PosRegisterDto>> GetById(Guid id, CancellationToken ct)
    {
        var register = await _db.PosRegisters
            .AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new PosRegisterDto(
                x.Id,
                x.OutletId,
                x.Outlet.Name,
                x.StockLocationId,
                x.Code,
                x.Name,
                x.IsActive,
                x.CreatedAt,
                x.UpdatedAt))
            .FirstOrDefaultAsync(ct);

        if (register is null)
            return NotFound("Register tidak ditemukan.");
        if (!CanAccessOutlet(register.OutletId))
            return Forbid();

        return Ok(register);
    }

    [HttpPost]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult<PosRegisterDto>> Create(
        [FromBody] CreatePosRegisterRequest request,
        CancellationToken ct)
    {
        var outlet = await _db.Outlets
            .FirstOrDefaultAsync(x => x.Id == request.OutletId && x.IsActive, ct);
        if (outlet is null)
            return BadRequest("Outlet tidak valid atau tidak aktif.");

        var stockLocationExists = await _db.StockLocations
            .AnyAsync(x => x.Id == outlet.Id && x.Type == StockLocationTypes.Outlet, ct);
        if (!stockLocationExists)
            return Conflict("Outlet belum memiliki stock location. Muat ulang data outlet.");

        var error = Validate(request.Code, request.Name);
        if (error is not null)
            return BadRequest(error);

        var code = request.Code.Trim().ToUpperInvariant();
        if (await _db.PosRegisters.AnyAsync(
                x => x.OutletId == outlet.Id && x.Code.ToLower() == code.ToLower(), ct))
            return Conflict("Kode register sudah digunakan pada outlet ini.");

        var register = new PosRegister
        {
            Id = Guid.NewGuid(),
            OutletId = outlet.Id,
            StockLocationId = outlet.Id,
            Code = code,
            Name = request.Name.Trim(),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.PosRegisters.Add(register);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetById), new { id = register.Id }, MapToDto(register, outlet.Name));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult<PosRegisterDto>> Update(
        Guid id,
        [FromBody] UpdatePosRegisterRequest request,
        CancellationToken ct)
    {
        var register = await _db.PosRegisters
            .Include(x => x.Outlet)
            .FirstOrDefaultAsync(x => x.Id == id, ct);
        if (register is null)
            return NotFound("Register tidak ditemukan.");

        var error = Validate(request.Code, request.Name);
        if (error is not null)
            return BadRequest(error);

        var code = request.Code.Trim().ToUpperInvariant();
        if (await _db.PosRegisters.AnyAsync(
                x => x.Id != id && x.OutletId == register.OutletId && x.Code.ToLower() == code.ToLower(), ct))
            return Conflict("Kode register sudah digunakan pada outlet ini.");

        if (!request.IsActive && register.IsActive)
        {
            var hasOpenSession = await _db.CashierSessions
                .AnyAsync(x => x.RegisterId == id && x.Status == CashierSessionStatus.Open, ct);
            if (hasOpenSession)
                return Conflict("Register tidak dapat dinonaktifkan karena masih memiliki sesi kasir terbuka.");

            var devices = await _db.PosDevices
                .Where(x => x.RegisterId == id && x.Status == PosDeviceStatuses.Active)
                .ToListAsync(ct);
            foreach (var device in devices)
            {
                device.Status = PosDeviceStatuses.Revoked;
                device.RevokedAtUtc = DateTime.UtcNow;
                device.RevokedBy = _currentUser.UserId;
            }
        }

        register.Code = code;
        register.Name = request.Name.Trim();
        register.IsActive = request.IsActive;
        register.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return Ok(MapToDto(register, register.Outlet.Name));
    }

    [HttpGet("{id:guid}/devices")]
    public async Task<ActionResult<IReadOnlyList<PosDeviceDto>>> GetDevices(Guid id, CancellationToken ct)
    {
        var register = await _db.PosRegisters.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (register is null)
            return NotFound("Register tidak ditemukan.");
        if (!CanAccessOutlet(register.OutletId))
            return Forbid();

        var devices = await _db.PosDevices
            .AsNoTracking()
            .Where(x => x.RegisterId == id)
            .OrderByDescending(x => x.Status == PosDeviceStatuses.Active)
            .ThenByDescending(x => x.ActivatedAtUtc)
            .Select(x => new PosDeviceDto(
                x.Id,
                x.RegisterId,
                x.InstallationId,
                x.Name,
                x.Platform,
                x.Status,
                x.AppVersion,
                x.ActivatedAtUtc,
                x.LastSeenAtUtc,
                x.RevokedAtUtc))
            .ToListAsync(ct);

        return Ok(devices);
    }

    [HttpPost("{id:guid}/pairing-codes")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult<PairingCodeDto>> CreatePairingCode(
        Guid id,
        [FromBody] CreatePairingCodeRequest? request,
        CancellationToken ct)
    {
        var register = await _db.PosRegisters
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.IsActive, ct);
        if (register is null)
            return NotFound("Register tidak ditemukan atau tidak aktif.");

        var configuredTtl = int.TryParse(_configuration["Pos:PairingCodeTtlMinutes"], out var ttl) ? ttl : 10;
        var ttlMinutes = Math.Clamp(request?.TtlMinutes ?? configuredTtl, 1, 60);
        var code = PosCredentials.CreatePairingCode();
        var expiresAt = DateTime.UtcNow.AddMinutes(ttlMinutes);

        var stale = await _db.PosDevicePairingCodes
            .Where(x => x.RegisterId == id && x.UsedAtUtc == null)
            .ToListAsync(ct);
        _db.PosDevicePairingCodes.RemoveRange(stale);

        _db.PosDevicePairingCodes.Add(new PosDevicePairingCode
        {
            Id = Guid.NewGuid(),
            RegisterId = id,
            CodeHash = PosCredentials.HashPairingCode(code),
            ExpiresAtUtc = expiresAt,
            CreatedBy = _currentUser.UserId ?? Guid.Empty,
            CreatedAtUtc = DateTime.UtcNow,
        });

        await _db.SaveChangesAsync(ct);

        return Ok(new PairingCodeDto(id, register.OutletId, code, expiresAt));
    }

    [HttpPost("{id:guid}/devices/{deviceId:guid}/revoke")]
    [Authorize(Roles = "Owner,Admin")]
    public async Task<ActionResult<PosDeviceDto>> RevokeDevice(Guid id, Guid deviceId, CancellationToken ct)
    {
        var device = await _db.PosDevices
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == deviceId && x.RegisterId == id, ct);
        if (device is null)
            return NotFound("Perangkat tidak ditemukan pada register ini.");

        if (device.Status == PosDeviceStatuses.Active)
        {
            var hasOpenSession = await _db.CashierSessions
                .AnyAsync(x => x.DeviceId == deviceId && x.Status == CashierSessionStatus.Open, ct);
            if (hasOpenSession)
                return Conflict("Perangkat tidak dapat dicabut karena masih memiliki sesi kasir terbuka.");

            var now = DateTime.UtcNow;
            await _db.PosDevices
                .Where(x => x.Id == deviceId && x.Status == PosDeviceStatuses.Active)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(x => x.Status, PosDeviceStatuses.Revoked)
                    .SetProperty(x => x.RevokedAtUtc, now)
                    .SetProperty(x => x.RevokedBy, _currentUser.UserId), ct);
        }

        return Ok(new PosDeviceDto(
            device.Id,
            device.RegisterId,
            device.InstallationId,
            device.Name,
            device.Platform,
            PosDeviceStatuses.Revoked,
            device.AppVersion,
            device.ActivatedAtUtc,
            device.LastSeenAtUtc,
            DateTime.UtcNow));
    }

    private bool CanAccessAllOutlets()
        => _currentUser.Role is "Owner" or "Admin" or "Keuangan";

    private bool CanAccessOutlet(Guid outletId)
        => CanAccessAllOutlets() || _currentUser.OutletId == outletId;

    private static string? Validate(string code, string name)
    {
        var trimmedCode = code?.Trim() ?? string.Empty;
        if (trimmedCode.Length < 2 || trimmedCode.Length > 30)
            return "Kode register harus berkisar antara 2 sampai 30 karakter.";
        if (!System.Text.RegularExpressions.Regex.IsMatch(trimmedCode, "^[a-zA-Z0-9\\-_]+$"))
            return "Kode register hanya boleh berisi huruf, angka, strip (-), dan underscore (_).";

        var trimmedName = name?.Trim() ?? string.Empty;
        if (trimmedName.Length < 2 || trimmedName.Length > 100)
            return "Nama register harus berkisar antara 2 sampai 100 karakter.";

        return null;
    }

    private static PosRegisterDto MapToDto(PosRegister register, string outletName)
        => new(
            register.Id,
            register.OutletId,
            outletName,
            register.StockLocationId,
            register.Code,
            register.Name,
            register.IsActive,
            register.CreatedAt,
            register.UpdatedAt);
}

public record PosRegisterDto(
    Guid Id,
    Guid OutletId,
    string OutletName,
    Guid StockLocationId,
    string Code,
    string Name,
    bool IsActive,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreatePosRegisterRequest(Guid OutletId, string Code, string Name);

public record UpdatePosRegisterRequest(string Code, string Name, bool IsActive);

public record PosDeviceDto(
    Guid Id,
    Guid RegisterId,
    Guid InstallationId,
    string Name,
    string Platform,
    string Status,
    string? AppVersion,
    DateTime ActivatedAtUtc,
    DateTime? LastSeenAtUtc,
    DateTime? RevokedAtUtc);

public record CreatePairingCodeRequest(int? TtlMinutes);

public record PairingCodeDto(
    Guid RegisterId,
    Guid OutletId,
    string Code,
    DateTime ExpiresAtUtc);
