using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Channels;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public sealed class GoBizClientConfigService : IGoBizClientConfigService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IGoBizConfigProvider _provider;
    private readonly IDataProtector _protector;

    public GoBizClientConfigService(
        AppDbContext dbContext,
        ICurrentUserService currentUser,
        IGoBizConfigProvider provider,
        IDataProtectionProvider protectionProvider)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _provider = provider;
        _protector = protectionProvider.CreateProtector("GoBiz.ClientSecret.v1");
    }

    public async Task<GoBizClientConfigDto> GetAsync(Guid businessId, CancellationToken ct = default)
    {
        EnsureCanAccess(businessId);
        var resolved = await _provider.GetByBusinessAsync(businessId, ct);

        var row = await _dbContext.GoBizClientConfigs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.BusinessId == businessId, ct);

        return new GoBizClientConfigDto(
            BusinessId: businessId,
            Environment: resolved.Environment,
            ClientId: resolved.Source == "Database" ? resolved.ClientId : string.Empty,
            HasClientSecret: !string.IsNullOrWhiteSpace(resolved.ClientId) && !string.IsNullOrWhiteSpace(resolved.ClientSecret),
            PartnerId: resolved.Source == "Database" ? resolved.PartnerId : string.Empty,
            AuthorizationUrl: resolved.AuthorizationUrl,
            TokenUrl: resolved.TokenUrl,
            ApiBaseUrl: resolved.ApiBaseUrl,
            RedirectUri: resolved.RedirectUri,
            Scope: resolved.Scope,
            UserType: resolved.UserType,
            Prompt: resolved.Prompt,
            HasWebhookSecret: !string.IsNullOrWhiteSpace(resolved.WebhookSecret),
            IsActive: row?.IsActive ?? false,
            Source: resolved.Source,
            UpdatedAt: row?.UpdatedAt);
    }

    public async Task<GoBizClientConfigDto> UpsertAsync(UpsertGoBizClientConfigRequest request, CancellationToken ct = default)
    {
        EnsureCanAccess(request.BusinessId);
        Validate(request);

        var businessExists = await _dbContext.Businesses.AsNoTracking()
            .AnyAsync(x => x.Id == request.BusinessId, ct);
        if (!businessExists)
            throw new InvalidOperationException("Business tidak valid.");

        var row = await _dbContext.GoBizClientConfigs
            .FirstOrDefaultAsync(x => x.BusinessId == request.BusinessId, ct);

        if (row is null)
        {
            if (string.IsNullOrWhiteSpace(request.ClientSecret))
                throw new InvalidOperationException("ClientSecret wajib diisi untuk config baru.");

            row = new GoBizClientConfig
            {
                Id = Guid.NewGuid(),
                BusinessId = request.BusinessId,
                CreatedAt = DateTime.UtcNow
            };
            _dbContext.GoBizClientConfigs.Add(row);
        }

        row.Environment = request.Environment.Trim();
        row.ClientId = request.ClientId.Trim();
        if (!string.IsNullOrWhiteSpace(request.ClientSecret))
            row.ClientSecretProtected = _protector.Protect(request.ClientSecret.Trim());
        row.PartnerId = request.PartnerId.Trim();
        row.AuthorizationUrl = request.AuthorizationUrl.Trim();
        row.TokenUrl = request.TokenUrl.Trim();
        row.ApiBaseUrl = request.ApiBaseUrl.Trim().TrimEnd('/');
        row.RedirectUri = request.RedirectUri.Trim();
        row.Scope = request.Scope.Trim();
        row.UserType = string.IsNullOrWhiteSpace(request.UserType) ? "merchant" : request.UserType.Trim();
        row.Prompt = string.IsNullOrWhiteSpace(request.Prompt) ? "login" : request.Prompt.Trim();
        if (!string.IsNullOrWhiteSpace(request.WebhookSecret))
            row.WebhookSecretProtected = _protector.Protect(request.WebhookSecret.Trim());
        else if (request.WebhookSecret is not null && request.WebhookSecret == string.Empty)
            row.WebhookSecretProtected = null; // eksplisit hapus
        row.IsActive = request.IsActive;
        row.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        return await GetAsync(request.BusinessId, ct);
    }

    public async Task<GoBizConfigDebugDto> GetDebugAsync(Guid? businessId, Guid? outletId, CancellationToken ct = default)
    {
        ResolvedGoBizConfig resolved;
        if (outletId.HasValue)
        {
            var outlet = await _dbContext.Outlets.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == outletId.Value, ct)
                ?? throw new InvalidOperationException("Outlet tidak valid.");
            EnsureCanAccess(outlet.BusinessId ?? businessId ?? Guid.Empty);
            resolved = await _provider.GetByOutletAsync(outletId.Value, ct);
        }
        else if (businessId.HasValue)
        {
            EnsureCanAccess(businessId.Value);
            resolved = await _provider.GetByBusinessAsync(businessId.Value, ct);
        }
        else
        {
            throw new InvalidOperationException("businessId atau outletId wajib diisi.");
        }

        return new GoBizConfigDebugDto(
            BusinessId: resolved.BusinessId ?? Guid.Empty,
            Environment: resolved.Environment,
            AuthorizationUrl: resolved.AuthorizationUrl,
            TokenUrl: resolved.TokenUrl,
            ApiBaseUrl: resolved.ApiBaseUrl,
            RedirectUri: resolved.RedirectUri,
            ClientIdConfigured: !string.IsNullOrWhiteSpace(resolved.ClientId),
            ClientSecretConfigured: !string.IsNullOrWhiteSpace(resolved.ClientSecret),
            PartnerIdConfigured: !string.IsNullOrWhiteSpace(resolved.PartnerId),
            WebhookSecretConfigured: !string.IsNullOrWhiteSpace(resolved.WebhookSecret),
            Source: resolved.Source);
    }

    private void EnsureCanAccess(Guid businessId)
    {
        if (!_currentUser.IsAuthenticated) throw new UnauthorizedAccessException("Belum login.");
        if (_currentUser.Role == "Owner") return;
        if (_currentUser.BusinessId.HasValue && _currentUser.BusinessId.Value == businessId) return;
        throw new UnauthorizedAccessException("Anda tidak memiliki akses ke business tersebut.");
    }

    private static void Validate(UpsertGoBizClientConfigRequest r)
    {
        var errors = new List<string>();
        if (r.BusinessId == Guid.Empty) errors.Add("BusinessId wajib diisi.");
        if (string.IsNullOrWhiteSpace(r.ClientId)) errors.Add("ClientId wajib diisi.");
        if (string.IsNullOrWhiteSpace(r.PartnerId)) errors.Add("PartnerId wajib diisi.");
        if (string.IsNullOrWhiteSpace(r.AuthorizationUrl)) errors.Add("AuthorizationUrl wajib diisi.");
        if (string.IsNullOrWhiteSpace(r.TokenUrl)) errors.Add("TokenUrl wajib diisi.");
        if (string.IsNullOrWhiteSpace(r.ApiBaseUrl)) errors.Add("ApiBaseUrl wajib diisi.");
        if (string.IsNullOrWhiteSpace(r.RedirectUri)) errors.Add("RedirectUri wajib diisi.");
        if (string.IsNullOrWhiteSpace(r.Scope)) errors.Add("Scope wajib diisi.");
        if (r.Environment is not ("Sandbox" or "Production")) errors.Add("Environment harus Sandbox atau Production.");

        foreach (var (name, url) in new[] { ("AuthorizationUrl", r.AuthorizationUrl), ("TokenUrl", r.TokenUrl), ("ApiBaseUrl", r.ApiBaseUrl), ("RedirectUri", r.RedirectUri) })
        {
            if (!string.IsNullOrWhiteSpace(url) && !Uri.TryCreate(url.Trim(), UriKind.Absolute, out _))
                errors.Add($"{name} harus absolute URL.");
        }
        if (!string.IsNullOrWhiteSpace(r.RedirectUri) && Uri.TryCreate(r.RedirectUri.Trim(), UriKind.Absolute, out var ru))
        {
            if (!string.IsNullOrEmpty(ru.Query)) errors.Add("RedirectUri tidak boleh mengandung query.");
            if (ru.Scheme != Uri.UriSchemeHttps && !string.Equals(ru.Host, "localhost", StringComparison.OrdinalIgnoreCase))
                errors.Add("RedirectUri harus HTTPS (localhost http hanya untuk dev).");
        }
        if (errors.Count > 0) throw new InvalidOperationException(string.Join(" ", errors));
    }
}
