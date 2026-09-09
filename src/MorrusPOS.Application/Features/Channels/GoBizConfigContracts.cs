namespace MorrusPOS.Application.Features.Channels;

/// <summary>
/// Opsi B: kredensial GoBiz per-Business tersimpan di DB.
/// Secret tidak pernah dikembalikan plain ke client; hanya flag Has*.
/// </summary>
public record GoBizClientConfigDto(
    Guid BusinessId,
    string Environment,
    string ClientId,
    bool HasClientSecret,
    string PartnerId,
    string AuthorizationUrl,
    string TokenUrl,
    string ApiBaseUrl,
    string RedirectUri,
    string Scope,
    string UserType,
    string Prompt,
    bool HasWebhookSecret,
    bool IsActive,
    string Source,
    DateTime? UpdatedAt
);

public record UpsertGoBizClientConfigRequest(
    Guid BusinessId,
    string Environment,
    string ClientId,
    string? ClientSecret,
    string PartnerId,
    string AuthorizationUrl,
    string TokenUrl,
    string ApiBaseUrl,
    string RedirectUri,
    string Scope,
    string? UserType,
    string? Prompt,
    string? WebhookSecret,
    bool IsActive
);

public record GoBizConfigDebugDto(
    Guid BusinessId,
    string Environment,
    string AuthorizationUrl,
    string TokenUrl,
    string ApiBaseUrl,
    string RedirectUri,
    bool ClientIdConfigured,
    bool ClientSecretConfigured,
    bool PartnerIdConfigured,
    bool WebhookSecretConfigured,
    string Source
);

/// <summary>
/// Hasil resolve config efektif untuk satu Business.
/// Source = Database | AppsettingsFallback (dari appsettings.json / Env Var GoBiz__*).
/// </summary>
public record ResolvedGoBizConfig(
    Guid? BusinessId,
    string Environment,
    string ClientId,
    string ClientSecret,
    string PartnerId,
    string AuthorizationUrl,
    string TokenUrl,
    string ApiBaseUrl,
    string RedirectUri,
    string Scope,
    string UserType,
    string Prompt,
    string? WebhookSecret,
    string Source,
    int RequestTimeoutSeconds,
    int TokenRefreshSkewSeconds
);

public interface IGoBizConfigProvider
{
    Task<ResolvedGoBizConfig> GetByBusinessAsync(Guid businessId, CancellationToken ct = default);
    Task<ResolvedGoBizConfig> GetByOutletAsync(Guid outletId, CancellationToken ct = default);
}

public interface IGoBizClientConfigService
{
    Task<GoBizClientConfigDto> GetAsync(Guid businessId, CancellationToken ct = default);
    Task<GoBizClientConfigDto> UpsertAsync(UpsertGoBizClientConfigRequest request, CancellationToken ct = default);
    Task<GoBizConfigDebugDto> GetDebugAsync(Guid? businessId, Guid? outletId, CancellationToken ct = default);
}
