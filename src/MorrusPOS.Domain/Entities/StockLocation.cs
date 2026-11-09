using MorrusPOS.Domain.Common;

namespace MorrusPOS.Domain.Entities;

public static class StockLocationTypes
{
    public const string Outlet = "outlet";
    public const string Warehouse = "warehouse";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Outlet, Warehouse
    };
}

/// <summary>
/// Lokasi fisik stok. Outlet selalu memiliki satu stock location bertipe
/// outlet; gudang adalah stock location bertipe warehouse yang tidak menjual.
/// </summary>
public class StockLocation : AuditableEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;

    public string Type { get; set; } = StockLocationTypes.Warehouse;
    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;

    // Hanya terisi untuk lokasi bertipe outlet. Id lokasi outlet sama dengan Id outlet.
    public Guid? OutletId { get; set; }
    public Outlet? Outlet { get; set; }

    public string? Address { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Penugasan eksplisit user ke lokasi stok (mis. staf Gudang yang memegang
/// beberapa gudang). Owner/Admin tidak membutuhkan baris di sini.
/// </summary>
public class UserStockLocationAccess : BaseEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid UserId { get; set; }
    public User User { get; set; } = default!;
    public Guid StockLocationId { get; set; }
    public StockLocation StockLocation { get; set; } = default!;
    public bool IsDefault { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class PosDeviceStatuses
{
    public const string Active = "active";
    public const string Revoked = "revoked";
}

public static class PosDevicePlatforms
{
    public const string Web = "web";
    public const string Android = "android";
    public const string Ios = "ios";
    public const string Windows = "windows";
    public const string MacOs = "macos";
    public const string Linux = "linux";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.Ordinal)
    {
        Web, Android, Ios, Windows, MacOs, Linux
    };
}

/// <summary>
/// Register/lajur kasir milik satu outlet. Stock yang dijual register berasal
/// dari stock location outlet tersebut.
/// </summary>
public class PosRegister : AuditableEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid OutletId { get; set; }
    public Outlet Outlet { get; set; } = default!;
    public Guid StockLocationId { get; set; }
    public StockLocation StockLocation { get; set; } = default!;

    public string Code { get; set; } = default!;
    public string Name { get; set; } = default!;
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Instalasi POS yang dipasangkan ke register. Raw credential hanya diberikan
/// sekali saat pairing; database menyimpan hash-nya.
/// </summary>
public class PosDevice : BaseEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid RegisterId { get; set; }
    public PosRegister Register { get; set; } = default!;

    public Guid InstallationId { get; set; }
    public string Name { get; set; } = default!;
    public string Platform { get; set; } = PosDevicePlatforms.Web;
    public string Status { get; set; } = PosDeviceStatuses.Active;
    public byte[] CredentialHash { get; set; } = default!;
    public string? AppVersion { get; set; }

    public DateTime ActivatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastSeenAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public Guid? RevokedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Kode pairing single-use. Raw code tidak pernah disimpan.
/// </summary>
public class PosDevicePairingCode : BaseEntity
{
    public Guid BusinessId { get; set; }
    public Business Business { get; set; } = default!;
    public Guid RegisterId { get; set; }
    public PosRegister Register { get; set; } = default!;

    public byte[] CodeHash { get; set; } = default!;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? UsedAtUtc { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public int FailedAttempts { get; set; }
}
