namespace MorrusPOS.Application.Common.Interfaces;

/// <summary>
/// Menyetel business tenant aktif untuk request saat ini pada alur tanpa JWT
/// (endpoint publik, webhook). Dipanggil SETELAH tenant berhasil di-resolve
/// dari kode publik/outlet — sebelum query apa pun lewat AppDbContext,
/// karena filter EF dan GUC PostgreSQL berbasis nilai ini (fail-closed).
/// </summary>
public interface ITenantContextSetter
{
    void SetBusinessOverride(Guid businessId);
}
