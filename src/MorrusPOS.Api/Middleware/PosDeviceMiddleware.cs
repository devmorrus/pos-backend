using Microsoft.EntityFrameworkCore;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Api.Middleware;

/// <summary>
/// Memvalidasi header device POS. Header opsional pada mode report_only,
/// tetapi kredensial yang dikirim selalu diverifikasi.
/// </summary>
public sealed class PosDeviceMiddleware
{
    public const string EnforcementReportOnly = "report_only";
    public const string EnforcementRequired = "required";

    private readonly RequestDelegate _next;
    private readonly ILogger<PosDeviceMiddleware> _logger;

    public PosDeviceMiddleware(RequestDelegate next, ILogger<PosDeviceMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(
        HttpContext context,
        AppDbContext db,
        ICurrentUserService currentUser,
        IConfiguration configuration)
    {
        if (!currentUser.IsAuthenticated)
        {
            await _next(context);
            return;
        }

        // Pairing harus tetap bisa memulihkan instalasi yang menyimpan kredensial lama/revoked.
        if (HttpMethods.IsPost(context.Request.Method)
            && context.Request.Path.Equals("/api/pos-devices/pair", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        var deviceIdValue = context.Request.Headers[PosCredentials.DeviceIdHeader].ToString();
        var token = context.Request.Headers[PosCredentials.DeviceTokenHeader].ToString();
        var hasDeviceId = !string.IsNullOrWhiteSpace(deviceIdValue);
        var hasToken = !string.IsNullOrWhiteSpace(token);

        if (!hasDeviceId && !hasToken)
        {
            var required = string.Equals(
                configuration["Pos:DeviceEnforcement"],
                EnforcementRequired,
                StringComparison.OrdinalIgnoreCase);
            if (required && RequiresDevice(context))
            {
                throw new UnauthorizedAccessException(
                    "Perangkat POS belum diaktifkan. Pasangkan perangkat ke register terlebih dahulu.");
            }

            if (!required && RequiresDevice(context))
            {
                _logger.LogWarning(
                    "PosDeviceBindingMissing Mode=report_only UserId={UserId} Path={Path}",
                    currentUser.UserId,
                    context.Request.Path);
            }

            await _next(context);
            return;
        }

        if (!hasDeviceId || !hasToken || !Guid.TryParse(deviceIdValue, out var deviceId))
        {
            throw new UnauthorizedAccessException("Header perangkat POS tidak valid.");
        }

        var device = await db.PosDevices
            .AsNoTracking()
            .Include(x => x.Register)
            .ThenInclude(x => x.Outlet)
            .FirstOrDefaultAsync(x => x.Id == deviceId, context.RequestAborted);

        if (device is null
            || device.Status != PosDeviceStatuses.Active
            || !device.Register.IsActive
            || !device.Register.Outlet.IsActive
            || !PosCredentials.Matches(device.CredentialHash, PosCredentials.HashDeviceToken(token)))
        {
            throw new UnauthorizedAccessException("Perangkat POS tidak valid atau sudah dicabut.");
        }

        context.Items[PosDeviceContext.DeviceIdKey] = device.Id;
        context.Items[PosDeviceContext.RegisterIdKey] = device.RegisterId;
        context.Items[PosDeviceContext.OutletIdKey] = device.Register.OutletId;
        context.Items[PosDeviceContext.StockLocationIdKey] = device.Register.StockLocationId;

        if (device.LastSeenAtUtc is null || device.LastSeenAtUtc < DateTime.UtcNow.AddMinutes(-5))
        {
            await db.PosDevices
                .Where(x => x.Id == device.Id && x.Status == PosDeviceStatuses.Active)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(x => x.LastSeenAtUtc, DateTime.UtcNow),
                    context.RequestAborted);
        }

        await _next(context);
    }

    private static bool RequiresDevice(HttpContext context)
        => context.Request.Path.StartsWithSegments("/api/cashier-sessions")
           || (HttpMethods.IsPost(context.Request.Method)
               && context.Request.Path.Equals("/api/transactions/checkout", StringComparison.OrdinalIgnoreCase));
}
