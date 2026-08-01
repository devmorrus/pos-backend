using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class Permission : BaseEntity
{
    public string Code { get; set; } = default!; // mis. "transaction.void", "product.price.edit"
    public string? Description { get; set; }

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

/// <summary>
/// Pivot table roles <-> permissions. PK komposit (RoleId, PermissionId).
/// </summary>
public class RolePermission
{
    public Guid RoleId { get; set; }
    public Role Role { get; set; } = default!;

    public Guid PermissionId { get; set; }
    public Permission Permission { get; set; } = default!;
}
