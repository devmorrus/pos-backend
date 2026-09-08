namespace MorrusPOS.Application.Features.Channels;

public record GoBizConnectUrlRequest(
    Guid OutletId
);

public record GoBizConnectUrlResponse(
    string AuthorizationUrl,
    string State,
    DateTime ExpiresAtUtc
);

public record GoBizConnectionStatusDto(
    Guid OutletId,
    bool IsConnected,
    bool IsActive,
    DateTime? ConnectedAtUtc,
    DateTime? ExpiresAtUtc,
    string? ExternalMerchantId,
    string? Scope
);

public record GoBizCallbackResult(
    bool Success,
    string Message,
    Guid OutletId
);

public interface IGoBizOAuthService
{
    Task<GoBizConnectUrlResponse> CreateConnectUrlAsync(Guid outletId, CancellationToken ct = default);
    Task<GoBizCallbackResult> HandleCallbackAsync(string code, string state, CancellationToken ct = default);
    Task<GoBizConnectionStatusDto> GetStatusAsync(Guid outletId, CancellationToken ct = default);
    Task DisconnectAsync(Guid outletId, CancellationToken ct = default);
    GoBizDebugConfigDto GetDebugConfig();
}

public record GoBizDebugConfigDto(
    string Environment,
    string RedirectUri,
    string AuthorizationUrl,
    string TokenUrl,
    bool ClientIdConfigured,
    bool ClientSecretConfigured
);
