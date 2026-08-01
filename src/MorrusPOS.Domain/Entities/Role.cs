using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class Role : BaseEntity
{
    public string Name { get; set; } = default!; // Owner, Admin, Kepala Cabang, Kasir, Gudang, Keuangan
    public string? Description { get; set; }

    public ICollection<User> Users { get; set; } = new List<User>();
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}
