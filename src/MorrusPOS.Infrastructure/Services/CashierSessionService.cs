using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Features.Transactions;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class CashierSessionService : ICashierSessionService
{
    private readonly AppDbContext _dbContext;

    public CashierSessionService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CashierSessionDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var session = await _dbContext.CashierSessions
            .Include(s => s.Outlet)
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (session == null)
        {
            throw new InvalidOperationException("Sesi kasir tidak ditemukan.");
        }

        return MapToDto(session);
    }

    public async Task<CashierSessionDto?> GetActiveSessionAsync(Guid userId, Guid outletId, CancellationToken ct = default)
    {
        var session = await _dbContext.CashierSessions
            .Include(s => s.Outlet)
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.OutletId == outletId && s.Status == CashierSessionStatus.Open, ct);

        return session != null ? MapToDto(session) : null;
    }

    public async Task<CashierSessionDto> OpenSessionAsync(Guid userId, Guid outletId, OpenSessionRequest request, CancellationToken ct = default)
    {
        // Check if there is already an active session for this user at this outlet
        var existing = await GetActiveSessionAsync(userId, outletId, ct);
        if (existing != null)
        {
            throw new InvalidOperationException("Anda masih memiliki sesi kasir yang aktif di outlet ini. Harap tutup sesi terlebih dahulu.");
        }

        var newSession = new CashierSession
        {
            Id = Guid.NewGuid(),
            OutletId = outletId,
            UserId = userId,
            OpeningTime = DateTime.UtcNow,
            OpeningCash = request.OpeningCash,
            ExpectedCash = request.OpeningCash,
            Status = CashierSessionStatus.Open,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.CashierSessions.Add(newSession);
        await _dbContext.SaveChangesAsync(ct);

        return await GetByIdAsync(newSession.Id, ct);
    }

    public async Task<CashierSessionDto> CloseSessionAsync(Guid sessionId, CloseSessionRequest request, CancellationToken ct = default)
    {
        var session = await _dbContext.CashierSessions
            .Include(s => s.Outlet)
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session == null)
        {
            throw new InvalidOperationException("Sesi kasir tidak ditemukan.");
        }

        if (session.Status == CashierSessionStatus.Closed)
        {
            throw new InvalidOperationException("Sesi kasir sudah ditutup.");
        }

        // Calculate expected cash in drawer (OpeningCash + Cash Payments)
        var cashSales = await _dbContext.Transactions
            .Where(t => t.CashierSessionId == sessionId && t.Status == TransactionStatus.Completed)
            .SelectMany(t => t.Payments)
            .Where(p => p.Method == PaymentMethod.Cash)
            .SumAsync(p => p.Amount, ct);

        session.ExpectedCash = session.OpeningCash + cashSales;
        session.ActualCash = request.ActualCash;
        session.Variance = request.ActualCash - session.ExpectedCash;
        session.Status = CashierSessionStatus.Closed;
        session.ClosingTime = DateTime.UtcNow;
        session.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        return await GetByIdAsync(sessionId, ct);
    }

    private static CashierSessionDto MapToDto(CashierSession s)
    {
        return new CashierSessionDto(
            s.Id,
            s.OutletId,
            s.Outlet?.Name ?? string.Empty,
            s.UserId,
            s.User?.Name ?? string.Empty,
            s.OpeningTime,
            s.ClosingTime,
            s.OpeningCash,
            s.ExpectedCash,
            s.ActualCash,
            s.Variance,
            s.Status
        );
    }
}
