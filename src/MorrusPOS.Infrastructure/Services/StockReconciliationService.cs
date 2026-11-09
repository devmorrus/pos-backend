using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using MorrusPOS.Application.Common.Exceptions;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Application.Common.Events;

namespace MorrusPOS.Infrastructure.Services;

public class StockReconciliationService : IStockReconciliationService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IStockEventPublisher _stockEvents;

    public StockReconciliationService(
        AppDbContext dbContext,
        ICurrentUserService currentUser,
        IStockEventPublisher stockEvents)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _stockEvents = stockEvents;
    }

    public async Task<StockReconciliationResultDto> PreviewAsync(
        Guid userId, Guid outletId, CancellationToken ct = default)
    {
        await EnsureAccessAsync(outletId, repair: false, ct);
        var startedAt = DateTime.UtcNow;
        var rows = await GetRowsAsync(outletId, apply: false, ct);
        var scanned = await GetScannedCountAsync(outletId, ct);
        return MapResult(null, outletId, StockReconciliationMode.Preview,
            StockReconciliationStatus.Completed, scanned, rows, startedAt, DateTime.UtcNow);
    }

    public async Task<StockReconciliationResultDto> RepairAsync(
        Guid userId, Guid outletId, string reason, CancellationToken ct = default)
    {
        await EnsureAccessAsync(outletId, repair: true, ct);
        var normalizedReason = reason?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason) || normalizedReason.Length < 5)
            throw new StockIntegrityException(StockIntegrityErrorCodes.ReconciliationScopeInvalid,
                "Alasan rekonsiliasi wajib diisi minimal 5 karakter.");

        var startedAt = DateTime.UtcNow;
        var preview = await GetRowsAsync(outletId, apply: false, ct);
        var scanned = await GetScannedCountAsync(outletId, ct);
        if (preview.Any(x => !x.Repairable))
        {
            var blockedRun = await SaveRunAsync(userId, outletId, normalizedReason,
                StockReconciliationStatus.Blocked, scanned, preview, ct);
            throw new StockIntegrityException(StockIntegrityErrorCodes.ReconciliationBlocked,
                $"Rekonsiliasi diblokir oleh {blockedRun.BlockedCount} masalah batch, serial, produk non-stok, atau saldo ledger negatif.");
        }

        IDbContextTransaction? transaction = null;
        if (_dbContext.Database.IsRelational())
            transaction = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            var repaired = await GetRowsAsync(outletId, apply: true, ct);
            var run = await SaveRunAsync(userId, outletId, normalizedReason,
                StockReconciliationStatus.Completed, scanned, repaired, ct);
            var repairedKeys = repaired
                .Where(x => x.Action is "updated_balance" or "inserted_balance")
                .Select(x => new StockEventKey(x.ProductId, x.ProductVariantId))
                .ToArray();
            if (repairedKeys.Length > 0)
            {
                await _stockEvents.EnqueueCurrentStockAsync(
                    outletId,
                    repairedKeys,
                    $"stock:reconciliation:{run.Id:N}",
                    ct);
            }
            if (transaction is not null) await transaction.CommitAsync(ct);

            return MapResult(run.Id, outletId, StockReconciliationMode.Repair,
                run.Status, scanned, repaired, startedAt, run.CompletedAt);
        }
        catch (PostgresException ex) when (ex.MessageText.Contains("STOCK_RECONCILIATION_BLOCKED", StringComparison.Ordinal))
        {
            if (transaction is not null) await transaction.RollbackAsync(ct);
            throw new StockIntegrityException(StockIntegrityErrorCodes.ReconciliationBlocked,
                "Rekonsiliasi diblokir karena kondisi stok berubah atau proyeksi tracking tidak konsisten.");
        }
        catch
        {
            if (transaction is not null) await transaction.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (transaction is not null) await transaction.DisposeAsync();
        }
    }

    public async Task<IReadOnlyList<StockReconciliationRunDto>> GetRunsAsync(
        Guid outletId, int take, CancellationToken ct = default)
    {
        await EnsureAccessAsync(outletId, repair: false, ct);
        return await _dbContext.StockReconciliationRuns.AsNoTracking()
            .Where(x => x.OutletId == outletId)
            .OrderByDescending(x => x.StartedAt)
            .Take(Math.Clamp(take, 1, 100))
            .Select(x => new StockReconciliationRunDto(
                x.Id, x.OutletId, x.Outlet.Name, x.RequestedBy, x.RequestedByUser.Name,
                x.Mode, x.Status, x.Reason, x.ScannedCount, x.MismatchCount,
                x.RepairableCount, x.RepairedCount, x.BlockedCount,
                x.ErrorCode, x.ErrorMessage, x.StartedAt, x.CompletedAt))
            .ToListAsync(ct);
    }

    public async Task<StockReconciliationResultDto> GetRunAsync(Guid id, CancellationToken ct = default)
    {
        var run = await _dbContext.StockReconciliationRuns.AsNoTracking()
            .Include(x => x.Items).ThenInclude(x => x.Product)
            .Include(x => x.Items).ThenInclude(x => x.ProductVariant)
            .SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Run rekonsiliasi tidak ditemukan.");
        await EnsureAccessAsync(run.OutletId, repair: false, ct);
        var rows = run.Items.Select(x => new StockReconciliationSqlRow
        {
            ProductId = x.ProductId,
            ProductVariantId = x.ProductVariantId,
            TrackingMode = x.TrackingMode,
            LedgerQty = x.LedgerQty,
            StoredQty = x.StoredQty,
            Difference = x.Difference,
            BatchQty = x.BatchQty,
            AvailableSerialCount = x.AvailableSerialCount,
            IssueType = x.IssueType,
            Repairable = x.Action != "blocked",
            Action = x.Action
        }).ToList();
        return MapResult(run.Id, run.OutletId, run.Mode, run.Status,
            run.ScannedCount, rows, run.StartedAt, run.CompletedAt);
    }

    public async Task<IReadOnlyList<StockReconciliationMismatchDto>> GetRunItemsAsync(
        Guid id, int page, int pageSize, CancellationToken ct = default)
    {
        var run = await _dbContext.StockReconciliationRuns.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Run rekonsiliasi tidak ditemukan.");
        await EnsureAccessAsync(run.OutletId, repair: false, ct);
        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, 200);
        return await _dbContext.StockReconciliationItems.AsNoTracking()
            .Where(x => x.RunId == id)
            .OrderByDescending(x => Math.Abs(x.Difference))
            .ThenBy(x => x.Product.Name)
            .Skip((safePage - 1) * safeSize)
            .Take(safeSize)
            .Select(x => new StockReconciliationMismatchDto(
                x.ProductId, x.ProductVariantId,
                x.ProductVariantId.HasValue ? x.ProductVariant!.Sku : x.Product.Sku,
                x.Product.Name, x.TrackingMode, x.LedgerQty, x.StoredQty,
                x.Difference, x.BatchQty, x.AvailableSerialCount,
                x.IssueType, x.Action != "blocked", x.Action))
            .ToListAsync(ct);
    }

    private async Task<List<StockReconciliationSqlRow>> GetRowsAsync(
        Guid outletId, bool apply, CancellationToken ct)
    {
        if (_dbContext.Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
            return await GetInMemoryRowsAsync(outletId, apply, ct);

        return await _dbContext.Database
            .SqlQuery<StockReconciliationSqlRow>(
                $"SELECT * FROM fn_reconcile_inventory_stock({outletId}, {apply})")
            .ToListAsync(ct);
    }

    private async Task<List<StockReconciliationSqlRow>> GetInMemoryRowsAsync(
        Guid outletId, bool apply, CancellationToken ct)
    {
        var ledger = await _dbContext.StockLedgers.AsNoTracking()
            .Where(x => x.OutletId == outletId)
            .GroupBy(x => new { x.ProductId, x.ProductVariantId })
            .Select(x => new { x.Key.ProductId, x.Key.ProductVariantId, Qty = x.Sum(v => v.QtyChange) })
            .ToListAsync(ct);
        var balances = await _dbContext.InventoryStocks
            .Where(x => x.OutletId == outletId)
            .ToListAsync(ct);
        var products = await _dbContext.Products.AsNoTracking()
            .ToDictionaryAsync(x => x.Id, ct);
        var batchTotals = await _dbContext.ProductBatches.AsNoTracking()
            .Where(x => x.OutletId == outletId)
            .GroupBy(x => new { x.ProductId, x.ProductVariantId })
            .Select(x => new { x.Key.ProductId, x.Key.ProductVariantId, Qty = x.Sum(v => v.QtyRemaining) })
            .ToListAsync(ct);
        var serialTotals = await _dbContext.ProductSerials.AsNoTracking()
            .Where(x => x.OutletId == outletId && x.Status == ProductSerialStatuses.Available)
            .GroupBy(x => new { x.ProductId, x.ProductVariantId })
            .Select(x => new { x.Key.ProductId, x.Key.ProductVariantId, Qty = x.Count() })
            .ToListAsync(ct);
        var keys = ledger.Select(x => (x.ProductId, x.ProductVariantId))
            .Concat(balances.Select(x => (x.ProductId, x.ProductVariantId))).Distinct().ToList();
        var rows = new List<StockReconciliationSqlRow>();

        foreach (var key in keys)
        {
            if (!products.TryGetValue(key.ProductId, out var product)) continue;
            var expected = ledger.FirstOrDefault(x => x.ProductId == key.ProductId && x.ProductVariantId == key.ProductVariantId)?.Qty ?? 0;
            var balance = balances.FirstOrDefault(x => x.ProductId == key.ProductId && x.ProductVariantId == key.ProductVariantId);
            var stored = balance?.QtyOnHand ?? 0;
            var batchQty = batchTotals.FirstOrDefault(x => x.ProductId == key.ProductId && x.ProductVariantId == key.ProductVariantId)?.Qty ?? 0;
            var serialQty = serialTotals.FirstOrDefault(x => x.ProductId == key.ProductId && x.ProductVariantId == key.ProductVariantId)?.Qty ?? 0;
            var issue = GetIssue(product.TrackingMode, expected, stored, batchQty, serialQty);
            if (issue is null) continue;
            var repairable = issue == "balance_mismatch";
            var action = "none";
            if (apply && repairable)
            {
                if (balance is null)
                {
                    _dbContext.InventoryStocks.Add(new InventoryStock
                    {
                        Id = Guid.NewGuid(), ProductId = key.ProductId, ProductVariantId = key.ProductVariantId,
                        OutletId = outletId, QtyOnHand = expected, MinStockAlert = 0, UpdatedAt = DateTime.UtcNow
                    });
                    action = "inserted_balance";
                }
                else
                {
                    balance.QtyOnHand = expected;
                    balance.UpdatedAt = DateTime.UtcNow;
                    action = "updated_balance";
                }
            }
            rows.Add(new StockReconciliationSqlRow
            {
                ProductId = key.ProductId, ProductVariantId = key.ProductVariantId,
                TrackingMode = product.TrackingMode, LedgerQty = expected, StoredQty = stored,
                Difference = expected - stored, BatchQty = batchQty, AvailableSerialCount = serialQty,
                IssueType = issue, Repairable = repairable, Action = action
            });
        }
        if (apply) await _dbContext.SaveChangesAsync(ct);
        return rows;
    }

    private static string? GetIssue(string mode, decimal ledger, decimal stored, decimal batch, int serials)
    {
        if (ledger < 0) return "negative_ledger";
        if (mode == ProductTrackingModes.None && (ledger != 0 || stored != 0)) return "untracked_product_nonzero";
        if (mode == ProductTrackingModes.Batch && batch != ledger) return "batch_projection_mismatch";
        if (mode == ProductTrackingModes.Serial && serials != ledger) return "serial_projection_mismatch";
        return ledger != stored ? "balance_mismatch" : null;
    }

    private async Task<int> GetScannedCountAsync(Guid outletId, CancellationToken ct)
    {
        var stockKeys = await _dbContext.InventoryStocks.AsNoTracking().Where(x => x.OutletId == outletId)
            .Select(x => new { x.ProductId, x.ProductVariantId }).ToListAsync(ct);
        var ledgerKeys = await _dbContext.StockLedgers.AsNoTracking().Where(x => x.OutletId == outletId)
            .Select(x => new { x.ProductId, x.ProductVariantId }).Distinct().ToListAsync(ct);
        return stockKeys.Select(x => (x.ProductId, x.ProductVariantId))
            .Concat(ledgerKeys.Select(x => (x.ProductId, x.ProductVariantId))).Distinct().Count();
    }

    private async Task<StockReconciliationRun> SaveRunAsync(
        Guid userId, Guid outletId, string reason, string status, int scanned,
        IReadOnlyList<StockReconciliationSqlRow> rows, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var run = new StockReconciliationRun
        {
            Id = Guid.NewGuid(), OutletId = outletId, RequestedBy = userId,
            Mode = StockReconciliationMode.Repair, Status = status, Reason = reason,
            ScannedCount = scanned, MismatchCount = rows.Count,
            RepairableCount = rows.Count(x => x.Repairable),
            RepairedCount = rows.Count(x => x.Action is "updated_balance" or "inserted_balance"),
            BlockedCount = rows.Count(x => !x.Repairable), StartedAt = now, CompletedAt = now
        };
        if (status == StockReconciliationStatus.Blocked)
        {
            run.ErrorCode = StockIntegrityErrorCodes.ReconciliationBlocked;
            run.ErrorMessage = "Terdapat proyeksi tracking atau saldo ledger yang tidak aman diperbaiki otomatis.";
        }
        foreach (var row in rows)
            run.Items.Add(new StockReconciliationItem
            {
                Id = Guid.NewGuid(), OutletId = outletId, ProductId = row.ProductId,
                ProductVariantId = row.ProductVariantId, TrackingMode = row.TrackingMode,
                LedgerQty = row.LedgerQty, StoredQty = row.StoredQty, Difference = row.Difference,
                BatchQty = row.BatchQty, AvailableSerialCount = row.AvailableSerialCount,
                IssueType = row.IssueType, Action = row.Repairable ? row.Action : "blocked", CreatedAt = now
            });
        _dbContext.StockReconciliationRuns.Add(run);
        await _dbContext.SaveChangesAsync(ct);
        return run;
    }

    private async Task EnsureAccessAsync(Guid outletId, bool repair, CancellationToken ct)
    {
        if (!await _dbContext.Outlets.AsNoTracking().AnyAsync(x => x.Id == outletId, ct))
            throw new StockIntegrityException(StockIntegrityErrorCodes.ReconciliationScopeInvalid,
                "Outlet rekonsiliasi tidak valid.");
        if (_currentUser.Role != "Owner" && _currentUser.OutletId != outletId)
            throw new UnauthorizedAccessException("Anda tidak memiliki akses ke outlet tersebut.");
        if (repair && _currentUser.Role is not ("Owner" or "Admin"))
            throw new UnauthorizedAccessException("Repair stok hanya dapat dilakukan Owner atau Admin.");
    }

    private StockReconciliationResultDto MapResult(
        Guid? runId, Guid outletId, string mode, string status, int scanned,
        IReadOnlyList<StockReconciliationSqlRow> rows, DateTime startedAt, DateTime? completedAt)
    {
        var productIds = rows.Select(r => r.ProductId).Distinct().ToList();
        var variantIds = rows.Where(r => r.ProductVariantId.HasValue)
            .Select(r => r.ProductVariantId!.Value).Distinct().ToList();
        var products = _dbContext.Products.AsNoTracking()
            .Where(x => productIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Sku, x.Name }).ToDictionary(x => x.Id);
        var variants = _dbContext.ProductVariants.AsNoTracking()
            .Where(x => variantIds.Contains(x.Id))
            .Select(x => new { x.Id, x.Sku }).ToDictionary(x => x.Id);
        var items = rows.Select(x =>
        {
            products.TryGetValue(x.ProductId, out var product);
            var sku = x.ProductVariantId.HasValue && variants.TryGetValue(x.ProductVariantId.Value, out var variant)
                ? variant.Sku : product?.Sku ?? string.Empty;
            return new StockReconciliationMismatchDto(x.ProductId, x.ProductVariantId, sku,
                product?.Name ?? string.Empty, x.TrackingMode, x.LedgerQty, x.StoredQty,
                x.Difference, x.BatchQty, x.AvailableSerialCount, x.IssueType, x.Repairable, x.Action);
        }).ToList();
        return new StockReconciliationResultDto(runId, outletId, mode, status, scanned,
            rows.Count, rows.Count(x => x.Repairable),
            rows.Count(x => x.Action is "updated_balance" or "inserted_balance"),
            rows.Count(x => !x.Repairable), startedAt, completedAt, items);
    }
}

public class StockReconciliationSqlRow
{
    public Guid ProductId { get; set; }
    public Guid? ProductVariantId { get; set; }
    public string TrackingMode { get; set; } = ProductTrackingModes.Quantity;
    public decimal LedgerQty { get; set; }
    public decimal StoredQty { get; set; }
    public decimal Difference { get; set; }
    public decimal BatchQty { get; set; }
    public int AvailableSerialCount { get; set; }
    public string IssueType { get; set; } = default!;
    public bool Repairable { get; set; }
    public string Action { get; set; } = "none";
}
