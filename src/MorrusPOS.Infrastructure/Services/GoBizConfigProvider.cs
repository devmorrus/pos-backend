using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MorrusPOS.Application.Features.Channels;
using MorrusPOS.Infrastructure.Options;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;

namespace MorrusPOS.Infrastructure.Services;

/// <summary>
/// Opsi B: satu pintu resolve kredensial GoBiz.
/// Prioritas: DB per-Business (gobiz_client_configs, IsActive) -> fallback appsettings/Env.
/// </summary>
public sealed class GoBizConfigProvider : IGoBizConfigProvider
{
    private readonly AppDbContext _dbContext;
    private readonly GoBizOptions _fallback;
    private readonly IDataProtector _protector;

    public GoBizConfigProvider(
        AppDbContext dbContext,
        IOptions<GoBizOptions> fallback,
        IDataProtectionProvider protectionProvider)
    {
        _dbContext = dbContext;
        _fallback = fallback.Value;
        _protector = protectionProvider.CreateProtector("GoBiz.ClientSecret.v1");
    }

    public async Task<ResolvedGoBizConfig> GetByOutletAsync(Guid outletId, CancellationToken ct = default)
    {
        var outlet = await _dbContext.Outlets.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == outletId, ct)
            ?? throw new InvalidOperationException("Outlet tidak valid.");

        if (outlet.BusinessId.HasValue)
            return await GetByBusinessAsync(outlet.BusinessId.Value, ct);

        return FromFallback(null);
    }

    public async Task<ResolvedGoBizConfig> GetByBusinessAsync(Guid businessId, CancellationToken ct = default)
    {
        var row = await _dbContext.GoBizClientConfigs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.BusinessId == businessId && x.IsActive, ct);

        if (row is not null)
        {
            return new ResolvedGoBizConfig(
                BusinessId: row.BusinessId,
                Environment: string.IsNullOrWhiteSpace(row.Environment) ? "Sandbox" : row.Environment,
                ClientId: row.ClientId,
                ClientSecret: Unprotect(row.ClientSecretProtected),
                PartnerId: row.PartnerId,
                AuthorizationUrl: row.AuthorizationUrl,
                TokenUrl: row.TokenUrl,
                ApiBaseUrl: row.ApiBaseUrl,
                RedirectUri: row.RedirectUri,
                Scope: string.IsNullOrWhiteSpace(row.Scope) ? "openid" : row.Scope,
                UserType: string.IsNullOrWhiteSpace(row.UserType) ? "merchant" : row.UserType,
                Prompt: string.IsNullOrWhiteSpace(row.Prompt) ? "login" : row.Prompt,
                WebhookSecret: string.IsNullOrWhiteSpace(row.WebhookSecretProtected) ? null : Unprotect(row.WebhookSecretProtected),
                Source: "Database",
                RequestTimeoutSeconds: _fallback.RequestTimeoutSeconds <= 0 ? 30 : _fallback.RequestTimeoutSeconds,
                TokenRefreshSkewSeconds: Math.Max(0, _fallback.TokenRefreshSkewSeconds));
        }

        return FromFallback(businessId);
    }

    private ResolvedGoBizConfig FromFallback(Guid? businessId)
    {
        return new ResolvedGoBizConfig(
            BusinessId: businessId,
            Environment: string.IsNullOrWhiteSpace(_fallback.Environment) ? "Sandbox" : _fallback.Environment,
            ClientId: _fallback.ClientId ?? string.Empty,
            ClientSecret: _fallback.ClientSecret ?? string.Empty,
            PartnerId: _fallback.PartnerId ?? string.Empty,
            AuthorizationUrl: _fallback.AuthorizationUrl ?? string.Empty,
            TokenUrl: _fallback.TokenUrl ?? string.Empty,
            ApiBaseUrl: _fallback.ApiBaseUrl ?? string.Empty,
            RedirectUri: _fallback.RedirectUri ?? string.Empty,
            Scope: string.IsNullOrWhiteSpace(_fallback.Scope) ? "openid" : _fallback.Scope,
            UserType: string.IsNullOrWhiteSpace(_fallback.UserType) ? "merchant" : _fallback.UserType,
            Prompt: string.IsNullOrWhiteSpace(_fallback.Prompt) ? "login" : _fallback.Prompt,
            WebhookSecret: string.IsNullOrWhiteSpace(_fallback.WebhookSecret) ? null : _fallback.WebhookSecret,
            Source: "AppsettingsFallback",
            RequestTimeoutSeconds: _fallback.RequestTimeoutSeconds <= 0 ? 30 : _fallback.RequestTimeoutSeconds,
            TokenRefreshSkewSeconds: Math.Max(0, _fallback.TokenRefreshSkewSeconds));
    }

    private string Unprotect(string protectedPayload)
    {
        try { return _protector.Unprotect(protectedPayload); }
        catch { return string.Empty; }
    }
}
