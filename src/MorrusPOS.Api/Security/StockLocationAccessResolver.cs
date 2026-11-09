using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Api.Security;

public sealed class StockLocationAccessResolver
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public StockLocationAccessResolver(AppDbContext db, ICurrentUserService currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    public async Task<Guid> ResolveAsync(
        Guid? stockLocationId,
        Guid? legacyOutletId,
        CancellationToken ct)
    {
        var id = stockLocationId ?? legacyOutletId ?? _currentUser.OutletId
            ?? throw new InvalidOperationException("Pilih lokasi stok terlebih dahulu.");
        await EnsureAsync(id, ct);
        return id;
    }

    public async Task EnsureAsync(Guid stockLocationId, CancellationToken ct)
    {
        var location = await _db.StockLocations.AsNoTracking()
            .Where(x => x.Id == stockLocationId && x.IsActive)
            .Select(x => new { x.OutletId })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Lokasi stok tidak valid atau tidak aktif.");

        if (_currentUser.Role is "Owner" or "Admin") return;
        if (location.OutletId == _currentUser.OutletId) return;

        var userId = _currentUser.UserId ?? Guid.Empty;
        if (await _db.UserStockLocationAccesses.AsNoTracking()
                .AnyAsync(x => x.UserId == userId && x.StockLocationId == stockLocationId, ct))
            return;

        throw new UnauthorizedAccessException("Anda tidak memiliki akses ke lokasi stok tersebut.");
    }
}
