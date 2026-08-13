using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public class User : AuditableEntity
{
    // Nullable = akses semua outlet (Owner). Keputusan arsitektur: 1 user = 1 outlet utama,
    // tidak pakai tabel pivot user_outlets.
    public Guid? OutletId { get; set; }
    public Outlet? Outlet { get; set; }

    public Guid? BusinessId { get; set; }
    public Business? Business { get; set; }

    public Guid RoleId { get; set; }
    public Role Role { get; set; } = default!;

    public string Name { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string PasswordHash { get; set; } = default!;
    public bool IsActive { get; set; } = true;
    public DateTime? LastLoginAt { get; set; }

    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
    public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
    public ICollection<CashFlow> CashFlowsCreated { get; set; } = new List<CashFlow>();
}
