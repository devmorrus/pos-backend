using System.Text.Json;
namespace MorrusPOS.Application.Features.Channels;

public record GoBizDirectConnectRequest(
    Guid OutletId,
    string GoBizOutletId,
    string? PartnerId = null
);

public record GoBizDirectStatusDto(
    Guid OutletId,
    string? GoBizOutletId,
    string Environment,
    bool IsConnected,
    bool IsActive,
    DateTime? LastCatalogPulledAtUtc,
    DateTime? LastCatalogSyncedAtUtc,
    string? LastCatalogSyncStatus,
    string? LastCatalogSyncMessage,
    DateTime? LastWebhookAtUtc
);

public record GoBizDirectTokenStatusDto(
    bool Success,
    string TokenType,
    string Scope,
    DateTime ExpiresAtUtc
);

public record GoBizExternalCatalogDto(
    Guid OutletId,
    string GoBizOutletId,
    JsonElement Catalog,
    DateTime PulledAtUtc
);

public record GoBizCatalogPreviewDto(
    Guid OutletId,
    string GoBizOutletId,
    int CategoryCount,
    int ItemCount,
    IReadOnlyList<string> ValidationErrors,
    JsonElement Payload
);

public record GoBizCatalogSyncResultDto(
    Guid OutletId,
    string GoBizOutletId,
    bool Success,
    string Message,
    int CategoryCount,
    int ItemCount,
    DateTime SyncedAtUtc,
    JsonElement? Response
);

public record GoBizIntegrationLogDto(
    Guid Id,
    string ServiceName,
    string? StatusCode,
    bool IsSuccess,
    string? ErrorMessage,
    DateTime CreatedAt
);

public record GoBizOrderInboxDto(
    Guid Id,
    string GoBizOrderId,
    Guid OutletId,
    string EventType,
    string Status,
    Guid? TransactionId,
    string? ErrorMessage,
    DateTime ReceivedAtUtc,
    DateTime? ProcessedAtUtc
);

public record GoBizWebhookOrderRequest(
    [property: System.Text.Json.Serialization.JsonPropertyName("outletId")] string OutletId,
    [property: System.Text.Json.Serialization.JsonPropertyName("orderId")] string OrderId,
    [property: System.Text.Json.Serialization.JsonPropertyName("eventType")] string EventType,
    [property: System.Text.Json.Serialization.JsonPropertyName("data")] JsonElement? Data = null
);

public interface IGoBizDirectAuthService
{
    Task<GoBizDirectAccessToken> GetAccessTokenAsync(Guid outletId, bool forceRefresh = false, CancellationToken ct = default);
    Task<GoBizDirectTokenStatusDto> TestTokenAsync(Guid outletId, CancellationToken ct = default);
}

public record GoBizDirectAccessToken(
    string AccessToken,
    string TokenType,
    string Scope,
    DateTime ExpiresAtUtc
);

public interface IGoBizApiClient
{
    Task<JsonDocument> GetCatalogAsync(Guid outletId, string goBizOutletId, CancellationToken ct = default);
    Task<JsonDocument> UpdateCatalogAsync(Guid outletId, string goBizOutletId, JsonElement payload, CancellationToken ct = default);
}

public interface IGoBizDirectIntegrationService
{
    Task<GoBizDirectStatusDto> GetStatusAsync(Guid outletId, CancellationToken ct = default);
    Task<GoBizDirectStatusDto> ConnectAsync(GoBizDirectConnectRequest request, CancellationToken ct = default);
    Task DisconnectAsync(Guid outletId, CancellationToken ct = default);
    Task<GoBizExternalCatalogDto> GetExternalCatalogAsync(Guid outletId, CancellationToken ct = default);
    Task<GoBizCatalogPreviewDto> PreviewCatalogAsync(Guid outletId, CancellationToken ct = default);
    Task<GoBizCatalogSyncResultDto> SyncCatalogAsync(Guid outletId, CancellationToken ct = default);
    Task<IReadOnlyList<GoBizIntegrationLogDto>> GetLogsAsync(Guid outletId, int take, CancellationToken ct = default);
    Task<IReadOnlyList<GoBizOrderInboxDto>> GetOrderInboxesAsync(Guid outletId, int take, CancellationToken ct = default);
}

public interface IGoBizOrderWebhookService
{
    Task ProcessWebhookAsync(string rawBody, IReadOnlyDictionary<string, string> headers, CancellationToken ct = default);
}
