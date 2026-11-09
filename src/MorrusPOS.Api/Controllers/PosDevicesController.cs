using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Api.Controllers;

[ApiController]
[Route("api/pos-devices")]
[Authorize]
public class PosDevicesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IPosDeviceContext _deviceContext;

    public PosDevicesController(AppDbContext db, IPosDeviceContext deviceContext)
    {
        _db = db;
        _deviceContext = deviceContext;
    }

    [HttpGet("current")]
    public async Task<ActionResult<PosDeviceDto>> GetCurrent(CancellationToken ct)
    {
        if (_deviceContext.DeviceId is not { } deviceId)
            return NotFound("Request ini tidak terikat ke perangkat POS.");

        var device = await _db.PosDevices
            .AsNoTracking()
            .Where(x => x.Id == deviceId)
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
            .FirstOrDefaultAsync(ct);

        return device is null ? NotFound("Perangkat POS tidak ditemukan.") : Ok(device);
    }

    [HttpPost("pair")]
    public async Task<ActionResult<PairedDeviceDto>> Pair(
        [FromBody] PairPosDeviceRequest request,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
            return BadRequest("Kode pairing wajib diisi.");
        if (request.InstallationId == Guid.Empty)
            return BadRequest("Installation ID perangkat wajib diisi.");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 100)
            return BadRequest("Nama perangkat wajib diisi (maksimal 100 karakter).");
        if (!PosDevicePlatforms.All.Contains(request.Platform ?? string.Empty))
            return BadRequest("Platform perangkat tidak valid.");
        if (request.AppVersion?.Trim().Length > 50)
            return BadRequest("Versi aplikasi maksimal 50 karakter.");

        var now = DateTime.UtcNow;
        var codeHash = PosCredentials.HashPairingCode(request.Code);

        await using var transaction = await _db.Database.BeginTransactionAsync(ct);

        var pairingCode = await _db.PosDevicePairingCodes
            .Include(x => x.Register)
            .ThenInclude(x => x.Outlet)
            .FirstOrDefaultAsync(x => x.CodeHash == codeHash, ct);

        if (pairingCode is null
            || pairingCode.UsedAtUtc is not null
            || pairingCode.ExpiresAtUtc <= now)
            return BadRequest("Kode pairing tidak valid atau sudah kedaluwarsa.");
        if (!pairingCode.Register.IsActive || !pairingCode.Register.Outlet.IsActive)
            return Conflict("Register untuk kode pairing ini tidak aktif.");

        var consumed = await _db.PosDevicePairingCodes
            .Where(x => x.Id == pairingCode.Id
                        && x.UsedAtUtc == null
                        && x.ExpiresAtUtc > now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UsedAtUtc, now), ct);
        if (consumed != 1)
            return Conflict("Kode pairing sudah digunakan.");

        var existing = await _db.PosDevices
            .FirstOrDefaultAsync(x => x.InstallationId == request.InstallationId, ct);

        if (existing is { Status: PosDeviceStatuses.Active })
            return Conflict("Instalasi ini sudah terpasang pada perangkat aktif.");
        if (existing is not null && existing.RegisterId != pairingCode.RegisterId)
        {
            var registerAlreadyPaired = await _db.PosDevices
                .AnyAsync(x => x.RegisterId == pairingCode.RegisterId
                               && x.Status == PosDeviceStatuses.Active, ct);
            if (registerAlreadyPaired)
                return Conflict("Register sudah memiliki perangkat aktif. Cabut perangkat lama terlebih dahulu.");

            existing.RegisterId = pairingCode.RegisterId;
        }
        else if (existing is null)
        {
            var registerAlreadyPaired = await _db.PosDevices
                .AnyAsync(x => x.RegisterId == pairingCode.RegisterId
                               && x.Status == PosDeviceStatuses.Active, ct);
            if (registerAlreadyPaired)
                return Conflict("Register sudah memiliki perangkat aktif. Cabut perangkat lama terlebih dahulu.");

            existing = new PosDevice
            {
                Id = Guid.NewGuid(),
                InstallationId = request.InstallationId,
                CreatedAtUtc = now,
            };
            _db.PosDevices.Add(existing);
        }

        var token = PosCredentials.CreateDeviceToken();
        existing.RegisterId = pairingCode.RegisterId;
        existing.Name = request.Name.Trim();
        existing.Platform = request.Platform!;
        existing.Status = PosDeviceStatuses.Active;
        existing.CredentialHash = PosCredentials.HashDeviceToken(token);
        existing.AppVersion = string.IsNullOrWhiteSpace(request.AppVersion) ? null : request.AppVersion.Trim();
        existing.ActivatedAtUtc = now;
        existing.RevokedAtUtc = null;
        existing.RevokedBy = null;

        await _db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Ok(new PairedDeviceDto(
            existing.Id,
            token,
            pairingCode.RegisterId,
            pairingCode.Register.OutletId,
            pairingCode.Register.StockLocationId));
    }
}

public record PairPosDeviceRequest(
    string Code,
    Guid InstallationId,
    string Name,
    string? Platform,
    string? AppVersion);

public record PairedDeviceDto(
    Guid DeviceId,
    string Token,
    Guid RegisterId,
    Guid OutletId,
    Guid StockLocationId);
