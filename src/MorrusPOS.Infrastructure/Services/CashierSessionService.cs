using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Capabilities;
using MorrusPOS.Application.Features.Transactions;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Application.Features.Accounting;
using Microsoft.EntityFrameworkCore.Storage;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class CashierSessionService : ICashierSessionService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICapabilityEvaluator? _capabilities;
    private readonly IAccountingIntegrationService? _accountingIntegrationService;
    private readonly IPosDeviceContext? _posDeviceContext;
    private readonly IStockReservationService? _stockReservations;

    public CashierSessionService(
        AppDbContext dbContext,
        ICurrentUserService currentUserService,
        ICapabilityEvaluator? capabilities = null,
        IAccountingIntegrationService? accountingIntegrationService = null,
        IPosDeviceContext? posDeviceContext = null,
        IStockReservationService? stockReservations = null)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _capabilities = capabilities;
        _accountingIntegrationService = accountingIntegrationService;
        _posDeviceContext = posDeviceContext;
        _stockReservations = stockReservations;
    }

    public async Task<CashierSessionDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var session = await _dbContext.CashierSessions
            .Include(s => s.Outlet)
            .Include(s => s.Register)
            .Include(s => s.Device)
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (session == null)
        {
            throw new InvalidOperationException("Sesi kasir tidak ditemukan.");
        }

        await EnsureSalesPosAsync(session.OutletId, ct);

        return await BuildSessionDtoAsync(session, ct);
    }

    public async Task<CashierSessionDto?> GetActiveSessionAsync(Guid userId, Guid outletId, CancellationToken ct = default)
    {
        await EnsureOutletAccessibleAsync(outletId, ct);
        await EnsureSalesPosAsync(outletId, ct);

        var session = await _dbContext.CashierSessions
            .Include(s => s.Outlet)
            .Include(s => s.Register)
            .Include(s => s.Device)
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == userId && s.OutletId == outletId && s.Status == CashierSessionStatus.Open, ct);

        return session != null ? await BuildSessionDtoAsync(session, ct) : null;
    }

    public async Task<CashierSessionDto> OpenSessionAsync(Guid userId, Guid outletId, OpenSessionRequest request, CancellationToken ct = default)
    {
        await EnsureOperationalRoleAsync();

        if (request.RegisterId.HasValue && _posDeviceContext?.RegisterId != request.RegisterId)
            throw new UnauthorizedAccessException("Register hanya dapat dipilih oleh perangkat yang sudah dipasangkan.");

        var registerId = _posDeviceContext?.RegisterId;
        PosRegister? register = null;
        if (registerId.HasValue)
        {
            register = await _dbContext.PosRegisters
                .Include(r => r.Outlet)
                .FirstOrDefaultAsync(r => r.Id == registerId.Value, ct);
            if (register is null || !register.IsActive || !register.Outlet.IsActive)
                throw new InvalidOperationException("Register tidak valid atau tidak aktif.");
            if (register.OutletId != outletId)
            {
                if (_posDeviceContext?.RegisterId != register.Id)
                    throw new UnauthorizedAccessException("Register tidak berada pada outlet kerja Anda.");
                outletId = register.OutletId;
            }

            if (_posDeviceContext?.RegisterId is { } deviceRegisterId && deviceRegisterId != register.Id)
                throw new UnauthorizedAccessException("Sesi harus dibuka pada register perangkat ini.");
        }

        await EnsureOutletAccessibleAsync(outletId, ct);
        await EnsureSalesPosAsync(outletId, ct);

        if (register is not null)
        {
            var registerInUse = await _dbContext.CashierSessions
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.Status == CashierSessionStatus.Open
                                          && s.RegisterId == register.Id, ct);
            if (registerInUse is not null)
                throw new InvalidOperationException(
                    $"Register {register.Name} sedang digunakan oleh {registerInUse.User?.Name ?? "kasir lain"}. Tutup sesi tersebut terlebih dahulu.");

            var userInUse = await _dbContext.CashierSessions
                .AnyAsync(s => s.Status == CashierSessionStatus.Open
                               && s.UserId == userId
                               && s.RegisterId != null, ct);
            if (userInUse)
                throw new InvalidOperationException("Anda masih memiliki sesi kasir aktif. Harap tutup sesi terlebih dahulu.");
        }
        else
        {
            // Legacy path (klien lama tanpa device): tetap satu sesi per user per outlet.
            var existing = await GetActiveSessionAsync(userId, outletId, ct);
            if (existing != null)
                throw new InvalidOperationException("Anda masih memiliki sesi kasir yang aktif di outlet ini. Harap tutup sesi terlebih dahulu.");
        }

        var newSession = new CashierSession
        {
            Id = Guid.NewGuid(),
            OutletId = outletId,
            UserId = userId,
            RegisterId = register?.Id,
            DeviceId = register is not null ? _posDeviceContext?.DeviceId : null,
            StockLocationId = register?.StockLocationId ?? outletId,
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
        await EnsureOperationalRoleAsync();

        var session = await _dbContext.CashierSessions
            .Include(s => s.Outlet)
            .Include(s => s.Register)
            .Include(s => s.Device)
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

        EnsureSessionDevice(session);

        if (_currentUserService.Role != "Owner")
        {
            if (_currentUserService.UserId != session.UserId || _currentUserService.OutletId != session.OutletId)
            {
                throw new UnauthorizedAccessException("Anda tidak memiliki akses untuk menutup sesi kasir ini.");
            }
        }

        await EnsureSalesPosAsync(session.OutletId, ct);

        if (_stockReservations is not null)
        {
            var activeReservationIds = await _dbContext.StockReservations.AsNoTracking()
                .Where(x => x.CashierSessionId == sessionId && x.Status == StockReservationStatus.Active)
                .Select(x => x.Id).ToListAsync(ct);
            foreach (var reservationId in activeReservationIds)
                await _stockReservations.ReleaseAsync(reservationId, "Sesi kasir ditutup.", ct);
        }

        var totalCashReceived = await _dbContext.Payments
            .Where(p => p.CashierSessionId == sessionId && p.Method == PaymentMethod.Cash)
            .SumAsync(p => p.Amount, ct);

        var totalPettyCashExpenses = await _dbContext.PettyCashExpenses
            .Where(pe => pe.CashierSessionId == sessionId)
            .SumAsync(pe => pe.Amount, ct);

        session.ExpectedCash = session.OpeningCash + totalCashReceived - totalPettyCashExpenses;
        session.ActualCash = request.ActualCash;
        session.Variance = request.ActualCash - session.ExpectedCash;
        session.Status = CashierSessionStatus.Closed;
        session.ClosingTime = DateTime.UtcNow;
        session.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        return await GetByIdAsync(sessionId, ct);
    }

    public async Task<PettyCashExpenseDto> RecordPettyCashAsync(Guid sessionId, CreatePettyCashRequest request, CancellationToken ct = default)
    {
        await EnsureOperationalRoleAsync();

        var session = await _dbContext.CashierSessions
            .Include(s => s.Outlet)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session == null)
        {
            throw new InvalidOperationException("Sesi kasir tidak ditemukan.");
        }

        if (session.Status == CashierSessionStatus.Closed)
        {
            throw new InvalidOperationException("Tidak dapat mencatat pengeluaran pada sesi kasir yang sudah ditutup.");
        }

        EnsureSessionDevice(session);

        if (_currentUserService.Role != "Owner")
        {
            if (_currentUserService.UserId != session.UserId || _currentUserService.OutletId != session.OutletId)
            {
                throw new UnauthorizedAccessException("Anda tidak memiliki akses untuk mencatat pengeluaran di sesi kasir ini.");
            }
        }

        await EnsureSalesPosAsync(session.OutletId, ct);

        if (request.Amount <= 0)
        {
            throw new InvalidOperationException("Nominal pengeluaran harus lebih dari 0.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new InvalidOperationException("Deskripsi pengeluaran wajib diisi.");
        }

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            throw new InvalidOperationException("Kategori pengeluaran wajib diisi.");
        }

        var expense = new PettyCashExpense
        {
            Id = Guid.NewGuid(),
            OutletId = session.OutletId,
            CashierSessionId = session.Id,
            Amount = request.Amount,
            Description = request.Description.Trim(),
            Category = request.Category.Trim(),
            ProcessedBy = _currentUserService.UserId ?? Guid.Empty,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        IDbContextTransaction? dbTx = null;
        if (_dbContext.Database.IsRelational()) dbTx = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            _dbContext.PettyCashExpenses.Add(expense);
            await _dbContext.SaveChangesAsync(ct);
            if (_accountingIntegrationService != null)
                await _accountingIntegrationService.EnsurePettyCashPostedAsync(expense.Id, ct);
            if (dbTx is not null) await dbTx.CommitAsync(ct);
        }
        catch
        {
            if (dbTx is not null) await dbTx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (dbTx is not null) await dbTx.DisposeAsync();
        }

        var saved = await _dbContext.PettyCashExpenses
            .Include(pe => pe.Outlet)
            .Include(pe => pe.ProcessedByUser)
            .FirstAsync(pe => pe.Id == expense.Id, ct);

        return MapPettyCashToDto(saved);
    }

    public async Task<IReadOnlyList<PettyCashExpenseDto>> GetPettyCashExpensesAsync(Guid sessionId, CancellationToken ct = default)
    {
        await EnsureOperationalRoleAsync();

        var session = await _dbContext.CashierSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session == null)
        {
            throw new InvalidOperationException("Sesi kasir tidak ditemukan.");
        }

        if (_currentUserService.Role != "Owner")
        {
            if (_currentUserService.UserId != session.UserId || _currentUserService.OutletId != session.OutletId)
            {
                throw new UnauthorizedAccessException("Anda tidak memiliki akses ke sesi kasir ini.");
            }
        }

        await EnsureSalesPosAsync(session.OutletId, ct);

        var expenses = await _dbContext.PettyCashExpenses
            .AsNoTracking()
            .Include(pe => pe.Outlet)
            .Include(pe => pe.ProcessedByUser)
            .Where(pe => pe.CashierSessionId == sessionId)
            .OrderByDescending(pe => pe.CreatedAt)
            .ToListAsync(ct);

        return expenses.Select(MapPettyCashToDto).ToList();
    }

    private async Task<CashierSessionDto> BuildSessionDtoAsync(CashierSession session, CancellationToken ct)
    {
        var payments = await _dbContext.Payments
            .Where(p => p.CashierSessionId == session.Id)
            .ToListAsync(ct);

        var totalCashReceived = payments
            .Where(p => p.Method == PaymentMethod.Cash)
            .Sum(p => p.Amount);

        var totalPettyCashExpenses = await _dbContext.PettyCashExpenses
            .Where(pe => pe.CashierSessionId == session.Id)
            .SumAsync(pe => pe.Amount, ct);

        var summary = payments
            .GroupBy(p => p.Method.ToLowerInvariant())
            .ToDictionary(g => g.Key, g => g.Sum(p => p.Amount));

        if (session.Status == CashierSessionStatus.Open)
        {
            session.ExpectedCash = session.OpeningCash + totalCashReceived - totalPettyCashExpenses;
        }

        return new CashierSessionDto(
            session.Id,
            session.OutletId,
            session.Outlet?.Name ?? string.Empty,
            session.UserId,
            session.User?.Name ?? string.Empty,
            session.OpeningTime,
            session.ClosingTime,
            session.OpeningCash,
            session.ExpectedCash,
            session.ActualCash,
            session.Variance,
            session.Status,
            totalCashReceived,
            totalPettyCashExpenses,
            summary
        )
        {
            RegisterId = session.RegisterId,
            RegisterName = session.Register?.Name,
            DeviceId = session.DeviceId,
            DeviceName = session.Device?.Name,
            StockLocationId = session.StockLocationId,
        };
    }

    private Task EnsureSalesPosAsync(Guid outletId, CancellationToken ct)
        => _capabilities?.EnsureEnabledAsync(CapabilityCodes.SalesPos, outletId, ct)
            ?? Task.CompletedTask;

    private static PettyCashExpenseDto MapPettyCashToDto(PettyCashExpense pe)
    {
        return new PettyCashExpenseDto(
            pe.Id,
            pe.OutletId,
            pe.Outlet?.Name ?? string.Empty,
            pe.CashierSessionId,
            pe.Amount,
            pe.Description,
            pe.Category,
            pe.ProcessedBy,
            pe.ProcessedByUser?.Name ?? string.Empty,
            pe.CreatedAt
        );
    }

    private Task EnsureOperationalRoleAsync()
    {
        if (_currentUserService.Role is "Owner" or "Admin" or "Kasir" or "KepalaCabang")
        {
            return Task.CompletedTask;
        }

        throw new UnauthorizedAccessException("Role Anda tidak memiliki akses ke POS kasir.");
    }

    private void EnsureSessionDevice(CashierSession session)
    {
        if (session.DeviceId.HasValue && _posDeviceContext?.DeviceId != session.DeviceId)
            throw new UnauthorizedAccessException("Sesi kasir terikat pada perangkat lain.");
        if (session.RegisterId.HasValue
            && _posDeviceContext?.RegisterId is { } registerId
            && registerId != session.RegisterId)
            throw new UnauthorizedAccessException("Sesi kasir terikat pada register lain.");
    }

    private async Task EnsureOutletAccessibleAsync(Guid outletId, CancellationToken ct)
    {
        var outlet = await _dbContext.Outlets
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == outletId, ct);

        if (outlet == null || !outlet.IsActive)
        {
            throw new InvalidOperationException("Outlet tidak valid atau tidak aktif.");
        }

        if (_currentUserService.Role != "Owner" && _currentUserService.OutletId != outletId)
        {
            throw new UnauthorizedAccessException("Anda tidak memiliki akses ke outlet tersebut.");
        }
    }
}
