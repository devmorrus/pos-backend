using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class AuditLog : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;

    public string EntityType { get; set; } = default!; // "transaction", "product", "stock", dll
    public Guid EntityId { get; set; }
    public string Action { get; set; } = default!; // "create", "update", "void", "price_change"

    public string? OldValueJson { get; set; } // disimpan sebagai jsonb di Postgres
    public string? NewValueJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
