using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Accounting;
using MorrusPOS.Application.Features.Channels;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class ChannelAccountService : IChannelAccountService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;

    public ChannelAccountService(AppDbContext dbContext, ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
    }

    public async Task<IReadOnlyList<ChannelAccountDto>> GetAsync(Guid? outletId, CancellationToken ct = default)
    {
        var query = _dbContext.ChannelAccounts
            .Include(account => account.Outlet)
            .AsNoTracking()
            .AsQueryable();

        if (outletId.HasValue)
        {
            await EnsureOutletAccessibleAsync(outletId.Value, ct);
            query = query.Where(account => account.OutletId == outletId.Value);
        }

        var result = await query
            .OrderBy(account => account.ChannelName)
            .ThenBy(account => account.Name)
            .ToListAsync(ct);

        return result.Select(MapAccountDto).ToList();
    }

    public async Task<ChannelAccountDto> CreateAsync(CreateChannelAccountRequest request, CancellationToken ct = default)
    {
        await EnsureOutletAccessibleAsync(request.OutletId, ct);

        var account = new ChannelAccount
        {
            Id = Guid.NewGuid(),
            OutletId = request.OutletId,
            Name = request.Name.Trim(),
            ChannelName = request.ChannelName.Trim().ToLowerInvariant(),
            MerchantId = string.IsNullOrWhiteSpace(request.MerchantId) ? request.Name.Trim() : request.MerchantId.Trim(),
            DefaultCommissionRate = request.DefaultCommissionRate,
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.ChannelAccounts.Add(account);
        await _dbContext.SaveChangesAsync(ct);

        await _dbContext.Entry(account).Reference(a => a.Outlet).LoadAsync(ct);
        return MapAccountDto(account);
    }

    public async Task<ChannelAccountDto> UpdateAsync(Guid id, UpdateChannelAccountRequest request, CancellationToken ct = default)
    {
        var account = await _dbContext.ChannelAccounts.Include(a => a.Outlet).FirstOrDefaultAsync(a => a.Id == id, ct);
        if (account == null)
        {
            throw new InvalidOperationException("Akun channel tidak ditemukan.");
        }

        await EnsureOutletAccessibleAsync(account.OutletId, ct);
        if (account.OutletId != request.OutletId)
        {
            await EnsureOutletAccessibleAsync(request.OutletId, ct);
        }

        account.OutletId = request.OutletId;
        account.Name = request.Name.Trim();
        account.ChannelName = request.ChannelName.Trim().ToLowerInvariant();
        account.MerchantId = string.IsNullOrWhiteSpace(request.MerchantId) ? request.Name.Trim() : request.MerchantId.Trim();
        account.DefaultCommissionRate = request.DefaultCommissionRate;
        account.IsActive = request.IsActive;
        account.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(ct);
        await _dbContext.Entry(account).Reference(a => a.Outlet).LoadAsync(ct);
        return MapAccountDto(account);
    }

    private static ChannelAccountDto MapAccountDto(ChannelAccount account)
    {
        return new ChannelAccountDto(
            account.Id,
            account.OutletId,
            account.Outlet?.Name ?? string.Empty,
            account.Name,
            account.ChannelName,
            account.MerchantId,
            account.DefaultCommissionRate,
            account.IsActive
        );
    }

    private async Task EnsureOutletAccessibleAsync(Guid outletId, CancellationToken ct)
    {
        var outlet = await _dbContext.Outlets.AsNoTracking().FirstOrDefaultAsync(o => o.Id == outletId, ct);
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

public class ChannelSettlementService : IChannelSettlementService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly IAccountingIntegrationService? _accountingIntegrationService;

    public ChannelSettlementService(
        AppDbContext dbContext,
        ICurrentUserService currentUserService,
        IAccountingIntegrationService? accountingIntegrationService = null)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _accountingIntegrationService = accountingIntegrationService;
    }

    public async Task<IReadOnlyList<ChannelSettlementListItemDto>> GetAsync(ChannelSettlementFilters filters, CancellationToken ct = default)
    {
        var query = _dbContext.ChannelSettlements
            .Include(settlement => settlement.ChannelAccount).ThenInclude(account => account.Outlet)
            .AsNoTracking()
            .AsQueryable();

        if (filters.OutletId.HasValue)
        {
            await EnsureOutletAccessibleAsync(filters.OutletId.Value, ct);
            query = query.Where(settlement => settlement.ChannelAccount.OutletId == filters.OutletId.Value);
        }

        if (filters.ChannelAccountId.HasValue)
        {
            query = query.Where(settlement => settlement.ChannelAccountId == filters.ChannelAccountId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filters.Status))
        {
            var status = filters.Status.Trim().ToLowerInvariant();
            query = query.Where(settlement => settlement.Status == status);
        }

        if (filters.DateFrom.HasValue)
        {
            var fromDate = filters.DateFrom.Value.Date;
            query = query.Where(settlement => settlement.SettlementDate >= fromDate);
        }

        if (filters.DateTo.HasValue)
        {
            var toDate = filters.DateTo.Value.Date.AddDays(1).AddTicks(-1);
            query = query.Where(settlement => settlement.SettlementDate <= toDate);
        }

        var result = await query
            .OrderByDescending(settlement => settlement.SettlementDate)
            .ToListAsync(ct);

        return result.Select(settlement => new ChannelSettlementListItemDto(
            settlement.Id,
            settlement.SettlementNumber,
            settlement.ChannelAccountId,
            settlement.ChannelAccount?.Name ?? string.Empty,
            settlement.ChannelAccount?.OutletId ?? Guid.Empty,
            settlement.ChannelAccount?.Outlet?.Name ?? string.Empty,
            settlement.SettlementDate,
            settlement.GrossAmount,
            settlement.CommissionAmount,
            settlement.NetAmount,
            settlement.Status
        )).ToList();
    }

    public async Task<ChannelSettlementDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var settlement = await _dbContext.ChannelSettlements
            .Include(s => s.ChannelAccount).ThenInclude(a => a.Outlet)
            .Include(s => s.CreatedByUser)
            .Include(s => s.Items).ThenInclude(item => item.Transaction)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (settlement == null)
        {
            throw new InvalidOperationException("Settlement channel tidak ditemukan.");
        }

        await EnsureOutletAccessibleAsync(settlement.ChannelAccount.OutletId, ct);
        return MapSettlementDto(settlement);
    }

    public async Task<IReadOnlyList<ChannelSettlementEligibleTransactionDto>> GetEligibleTransactionsAsync(
        Guid channelAccountId,
        DateTime periodStartDate,
        DateTime periodEndDate,
        Guid? excludeSettlementId,
        CancellationToken ct = default)
    {
        var channelAccount = await _dbContext.ChannelAccounts.Include(a => a.Outlet).FirstOrDefaultAsync(a => a.Id == channelAccountId, ct);
        if (channelAccount == null)
        {
            throw new InvalidOperationException("Akun channel tidak ditemukan.");
        }

        if (!channelAccount.IsActive)
        {
            throw new InvalidOperationException("Akun channel tidak aktif.");
        }

        await EnsureOutletAccessibleAsync(channelAccount.OutletId, ct);

        var endDate = periodEndDate.Date.AddDays(1).AddTicks(-1);
        var usedTransactionIds = await _dbContext.ChannelSettlementItems
            .Where(item => item.ChannelSettlementId != excludeSettlementId)
            .Where(item => item.ChannelSettlement.Status != ChannelSettlementStatus.Cancelled)
            .Select(item => item.TransactionId)
            .ToListAsync(ct);

        var transactions = await _dbContext.Transactions
            .Include(transaction => transaction.User)
            .Where(transaction => transaction.OutletId == channelAccount.OutletId)
            .Where(transaction => transaction.Status == TransactionStatus.Completed)
            .Where(transaction => transaction.Channel == channelAccount.ChannelName)
            .Where(transaction => transaction.CreatedAt >= periodStartDate.Date && transaction.CreatedAt <= endDate)
            .Where(transaction => !transaction.Returns.Any())
            .Where(transaction => !usedTransactionIds.Contains(transaction.Id))
            .OrderByDescending(transaction => transaction.CreatedAt)
            .ToListAsync(ct);

        return transactions.Select(transaction => new ChannelSettlementEligibleTransactionDto(
            transaction.Id,
            transaction.TransactionNumber,
            transaction.OutletId,
            channelAccount.Outlet.Name,
            transaction.CreatedAt,
            transaction.GrandTotal,
            transaction.Channel,
            transaction.User?.Name ?? string.Empty
        )).ToList();
    }

    public async Task<ChannelSettlementDto> CreateAsync(Guid userId, CreateChannelSettlementRequest request, CancellationToken ct = default)
    {
        var channelAccount = await GetValidatedAccountAsync(request.ChannelAccountId, ct);
        var transactions = await GetValidatedTransactionsAsync(channelAccount, request.TransactionIds, request.PeriodStartDate, request.PeriodEndDate, null, ct);
        var commissionAmount = request.CommissionAmountOverride ?? CalculateCommission(transactions.Sum(transaction => transaction.GrandTotal), channelAccount.DefaultCommissionRate);

        var settlement = new ChannelSettlement
        {
            Id = Guid.NewGuid(),
            ChannelAccountId = channelAccount.Id,
            SettlementNumber = $"CHSET-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}",
            SettlementDate = DateTime.UtcNow,
            PeriodStartDate = request.PeriodStartDate.Date,
            PeriodEndDate = request.PeriodEndDate.Date,
            GrossAmount = transactions.Sum(transaction => transaction.GrandTotal),
            CommissionAmount = commissionAmount,
            NetAmount = transactions.Sum(transaction => transaction.GrandTotal) - commissionAmount,
            Status = ChannelSettlementStatus.Pending,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        settlement.Items = transactions.Select(transaction => new ChannelSettlementItem
        {
            Id = Guid.NewGuid(),
            ChannelSettlementId = settlement.Id,
            TransactionId = transaction.Id,
            GrossAmount = transaction.GrandTotal,
            CommissionAmount = 0,
            NetAmount = transaction.GrandTotal
        }).ToList();

        DistributeCommission(settlement);

        _dbContext.ChannelSettlements.Add(settlement);
        await _dbContext.SaveChangesAsync(ct);

        return await GetByIdAsync(settlement.Id, ct);
    }

    public async Task<ChannelSettlementDto> UpdateAsync(Guid userId, Guid id, UpdateChannelSettlementRequest request, CancellationToken ct = default)
    {
        var settlement = await _dbContext.ChannelSettlements
            .Include(s => s.ChannelAccount)
            .Include(s => s.Items)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (settlement == null)
        {
            throw new InvalidOperationException("Settlement channel tidak ditemukan.");
        }

        await EnsureOutletAccessibleAsync(settlement.ChannelAccount.OutletId, ct);

        if (settlement.Status != ChannelSettlementStatus.Pending)
        {
            throw new InvalidOperationException("Hanya settlement pending yang dapat diubah.");
        }

        var transactions = await GetValidatedTransactionsAsync(settlement.ChannelAccount, request.TransactionIds, request.PeriodStartDate, request.PeriodEndDate, settlement.Id, ct);

        _dbContext.ChannelSettlementItems.RemoveRange(settlement.Items);
        settlement.Items.Clear();

        var commissionAmount = request.CommissionAmountOverride ?? CalculateCommission(transactions.Sum(transaction => transaction.GrandTotal), settlement.ChannelAccount.DefaultCommissionRate);

        settlement.PeriodStartDate = request.PeriodStartDate.Date;
        settlement.PeriodEndDate = request.PeriodEndDate.Date;
        settlement.GrossAmount = transactions.Sum(transaction => transaction.GrandTotal);
        settlement.CommissionAmount = commissionAmount;
        settlement.NetAmount = settlement.GrossAmount - commissionAmount;
        settlement.UpdatedAt = DateTime.UtcNow;

        settlement.Items = transactions.Select(transaction => new ChannelSettlementItem
        {
            Id = Guid.NewGuid(),
            ChannelSettlementId = settlement.Id,
            TransactionId = transaction.Id,
            GrossAmount = transaction.GrandTotal,
            CommissionAmount = 0,
            NetAmount = transaction.GrandTotal
        }).ToList();

        DistributeCommission(settlement);
        await _dbContext.SaveChangesAsync(ct);

        return await GetByIdAsync(settlement.Id, ct);
    }

    public async Task<ChannelSettlementDto> UpdateStatusAsync(Guid userId, Guid id, UpdateChannelSettlementStatusRequest request, CancellationToken ct = default)
    {
        var settlement = await _dbContext.ChannelSettlements
            .Include(s => s.ChannelAccount)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

        if (settlement == null)
        {
            throw new InvalidOperationException("Settlement channel tidak ditemukan.");
        }

        await EnsureOutletAccessibleAsync(settlement.ChannelAccount.OutletId, ct);

        if (settlement.Status != ChannelSettlementStatus.Pending)
        {
            throw new InvalidOperationException("Hanya settlement pending yang dapat diubah statusnya.");
        }

        var targetStatus = request.Status.Trim().ToLowerInvariant();
        settlement.Status = targetStatus;
        settlement.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
        if (targetStatus == ChannelSettlementStatus.Settled && _accountingIntegrationService != null)
        {
            await _accountingIntegrationService.EnsureChannelSettlementPostedAsync(settlement.Id, ct);
        }

        return await GetByIdAsync(id, ct);
    }

    private async Task<ChannelAccount> GetValidatedAccountAsync(Guid channelAccountId, CancellationToken ct)
    {
        var channelAccount = await _dbContext.ChannelAccounts.Include(a => a.Outlet).FirstOrDefaultAsync(a => a.Id == channelAccountId, ct);
        if (channelAccount == null)
        {
            throw new InvalidOperationException("Akun channel tidak ditemukan.");
        }

        if (!channelAccount.IsActive)
        {
            throw new InvalidOperationException("Akun channel tidak aktif.");
        }

        await EnsureOutletAccessibleAsync(channelAccount.OutletId, ct);
        return channelAccount;
    }

    private async Task<List<Transaction>> GetValidatedTransactionsAsync(
        ChannelAccount channelAccount,
        IReadOnlyCollection<Guid> transactionIds,
        DateTime periodStartDate,
        DateTime periodEndDate,
        Guid? excludeSettlementId,
        CancellationToken ct)
    {
        var eligibleTransactions = await GetEligibleTransactionsAsync(channelAccount.Id, periodStartDate, periodEndDate, excludeSettlementId, ct);
        var eligibleLookup = eligibleTransactions.ToDictionary(transaction => transaction.TransactionId);
        var invalidTransactionId = transactionIds.FirstOrDefault(id => !eligibleLookup.ContainsKey(id));
        if (invalidTransactionId != Guid.Empty)
        {
            throw new InvalidOperationException("Ada transaksi settlement yang tidak valid atau sudah dipakai di settlement lain.");
        }

        return await _dbContext.Transactions
            .Where(transaction => transactionIds.Contains(transaction.Id))
            .OrderBy(transaction => transaction.CreatedAt)
            .ToListAsync(ct);
    }

    private static decimal CalculateCommission(decimal grossAmount, decimal rate)
    {
        return Math.Round(grossAmount * (rate / 100m), 2, MidpointRounding.AwayFromZero);
    }

    private static void DistributeCommission(ChannelSettlement settlement)
    {
        if (settlement.Items.Count == 0)
        {
            return;
        }

        decimal allocated = 0;
        for (var index = 0; index < settlement.Items.Count; index++)
        {
            var item = settlement.Items.ElementAt(index);
            if (index == settlement.Items.Count - 1)
            {
                item.CommissionAmount = settlement.CommissionAmount - allocated;
            }
            else
            {
                item.CommissionAmount = settlement.GrossAmount == 0
                    ? 0
                    : Math.Round(settlement.CommissionAmount * (item.GrossAmount / settlement.GrossAmount), 2, MidpointRounding.AwayFromZero);
            }

            allocated += item.CommissionAmount;
            item.NetAmount = item.GrossAmount - item.CommissionAmount;
        }
    }

    private static ChannelSettlementDto MapSettlementDto(ChannelSettlement settlement)
    {
        return new ChannelSettlementDto(
            settlement.Id,
            settlement.SettlementNumber,
            settlement.ChannelAccountId,
            settlement.ChannelAccount?.Name ?? string.Empty,
            settlement.ChannelAccount?.ChannelName ?? string.Empty,
            settlement.ChannelAccount?.OutletId ?? Guid.Empty,
            settlement.ChannelAccount?.Outlet?.Name ?? string.Empty,
            settlement.SettlementDate,
            settlement.PeriodStartDate,
            settlement.PeriodEndDate,
            settlement.GrossAmount,
            settlement.CommissionAmount,
            settlement.NetAmount,
            settlement.ChannelAccount?.DefaultCommissionRate ?? 0,
            settlement.Status,
            settlement.CreatedBy,
            settlement.CreatedByUser?.Name ?? string.Empty,
            settlement.Items.Select(item => new ChannelSettlementItemDto(
                item.TransactionId,
                item.Transaction?.TransactionNumber ?? string.Empty,
                item.Transaction?.CreatedAt ?? DateTime.MinValue,
                item.GrossAmount,
                item.CommissionAmount,
                item.NetAmount
            )).ToList()
        );
    }

    private async Task EnsureOutletAccessibleAsync(Guid outletId, CancellationToken ct)
    {
        var outlet = await _dbContext.Outlets.AsNoTracking().FirstOrDefaultAsync(o => o.Id == outletId, ct);
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
