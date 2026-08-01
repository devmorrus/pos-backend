namespace MorrusPOS.Domain.Common;

/// <summary>
/// Base class untuk semua entity dengan primary key UUID.
/// Semua tabel di skema MorrusPOS pakai UUID sebagai PK.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
}

/// <summary>
/// Base class untuk entity yang punya CreatedAt/UpdatedAt.
/// Sebagian besar tabel transaksional & master data pakai ini.
/// </summary>
public abstract class AuditableEntity : BaseEntity
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
