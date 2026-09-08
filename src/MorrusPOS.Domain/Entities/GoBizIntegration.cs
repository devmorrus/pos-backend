using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class GoBizIntegration : AuditableEntity
{
    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;

    public Guid? BusinessId { get; set; }
    public Business? Business { get; set; }

    public string? ExternalMerchantId { get; set; }
    public string AccessToken { get; set; } = default!;
    public string? RefreshToken { get; set; }
    public string? TokenType { get; set; }
    public string? Scope { get; set; }
    public DateTime? ExpiresAtUtc { get; set; }
    public DateTime ConnectedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSyncedAtUtc { get; set; }
    public bool IsActive { get; set; } = true;
    public string? RawMetadataJson { get; set; }
}

public class GoBizOAuthState : AuditableEntity
{
    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;

    public Guid? BusinessId { get; set; }
    public Business? Business { get; set; }

    public string State { get; set; } = default!;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
}

public class GoBizDirectIntegration : AuditableEntity
{
    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;

    public Guid? BusinessId { get; set; }
    public Business? Business { get; set; }

    public string PartnerId { get; set; } = default!;
    public string GoBizOutletId { get; set; } = default!;
    public string Environment { get; set; } = "Sandbox";
    public bool IsActive { get; set; } = true;

    public DateTime? LastCatalogPulledAtUtc { get; set; }
    public DateTime? LastCatalogSyncedAtUtc { get; set; }
    public string? LastCatalogSyncStatus { get; set; }
    public string? LastCatalogSyncMessage { get; set; }
    public DateTime? LastWebhookAtUtc { get; set; }
}

public class GoBizProductMapping : AuditableEntity
{
    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;

    public Guid ProductId { get; set; }
    public Product Product { get; set; } = default!;

    public Guid? ProductVariantId { get; set; }
    public ProductVariant? ProductVariant { get; set; }

    public string GoBizItemId { get; set; } = default!;
    public string? GoBizCategoryId { get; set; }
    public bool IsSynced { get; set; }
    public DateTime? LastSyncedAtUtc { get; set; }
    public string? LastSyncError { get; set; }
}

public class GoBizOrderInbox : BaseEntity
{
    public string GoBizOrderId { get; set; } = default!;
    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;
    public string EventType { get; set; } = default!;
    public string RawPayloadJson { get; set; } = default!;
    public string Status { get; set; } = "received";
    public Guid? TransactionId { get; set; }
    public Transaction? Transaction { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAtUtc { get; set; }
}
