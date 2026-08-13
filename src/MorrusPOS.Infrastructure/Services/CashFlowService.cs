using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Accounting;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class CashFlowService : ICashFlowService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly ICashFlowPostingService _postingService;

    public CashFlowService(
        AppDbContext dbContext,
        ICurrentUserService currentUserService,
        ICashFlowPostingService postingService)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _postingService = postingService;
    }

    public async Task<IReadOnlyList<CashFlowListItemDto>> GetAsync(CashFlowFilters filters, CancellationToken ct = default)
    {
        EnsureBusinessContext();

        var query = _dbContext.CashFlows
            .Include(cashFlow => cashFlow.FromChartOfAccount)
            .Include(cashFlow => cashFlow.ToChartOfAccount)
            .Include(cashFlow => cashFlow.Outlet)
            .Include(cashFlow => cashFlow.CreatedByUser)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filters.TrxType))
        {
            var trxType = filters.TrxType.Trim().ToLowerInvariant();
            query = query.Where(cashFlow => cashFlow.TrxType == trxType);
        }

        if (filters.OutletId.HasValue)
        {
            await EnsureOutletAccessibleAsync(filters.OutletId.Value, ct);
            query = query.Where(cashFlow => cashFlow.OutletId == filters.OutletId.Value);
        }

        if (filters.DateFrom.HasValue)
        {
            var start = DateTime.SpecifyKind(filters.DateFrom.Value.Date, DateTimeKind.Utc);
            query = query.Where(cashFlow => cashFlow.TrxDate >= start);
        }

        if (filters.DateTo.HasValue)
        {
            var end = DateTime.SpecifyKind(filters.DateTo.Value.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);
            query = query.Where(cashFlow => cashFlow.TrxDate <= end);
        }

        if (filters.ChartOfAccountId.HasValue)
        {
            query = query.Where(cashFlow =>
                cashFlow.FromChartOfAccountId == filters.ChartOfAccountId.Value
                || cashFlow.ToChartOfAccountId == filters.ChartOfAccountId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filters.Keyword))
        {
            var keyword = filters.Keyword.Trim().ToLowerInvariant();
            query = query.Where(cashFlow =>
                cashFlow.TrxNumber.ToLower().Contains(keyword)
                || (cashFlow.Note != null && cashFlow.Note.ToLower().Contains(keyword))
                || (cashFlow.FromChartOfAccount != null && (
                    cashFlow.FromChartOfAccount.AccountCode.ToLower().Contains(keyword)
                    || cashFlow.FromChartOfAccount.AccountName.ToLower().Contains(keyword)))
                || (cashFlow.ToChartOfAccount != null && (
                    cashFlow.ToChartOfAccount.AccountCode.ToLower().Contains(keyword)
                    || cashFlow.ToChartOfAccount.AccountName.ToLower().Contains(keyword))));
        }

        var cashFlows = await query
            .OrderByDescending(cashFlow => cashFlow.TrxDate)
            .ThenByDescending(cashFlow => cashFlow.CreatedAt)
            .ToListAsync(ct);

        return cashFlows.Select(MapListItem).ToList();
    }

    public async Task<CashFlowDetailDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        EnsureBusinessContext();

        var cashFlow = await _dbContext.CashFlows
            .Include(current => current.FromChartOfAccount)
            .Include(current => current.ToChartOfAccount)
            .Include(current => current.Outlet)
            .Include(current => current.CreatedByUser)
            .AsNoTracking()
            .FirstOrDefaultAsync(current => current.Id == id, ct);

        if (cashFlow == null)
        {
            throw new InvalidOperationException("Transaksi cash flow tidak ditemukan.");
        }

        if (cashFlow.OutletId.HasValue)
        {
            await EnsureOutletAccessibleAsync(cashFlow.OutletId.Value, ct);
        }

        var journalEntries = await _dbContext.AccountTransactions
            .Include(entry => entry.ChartOfAccount)
            .AsNoTracking()
            .Where(entry => entry.ReferenceType == "cash_flow" && entry.ReferenceId == id)
            .OrderByDescending(entry => entry.DebitAmount)
            .ThenBy(entry => entry.ChartOfAccount.AccountCode)
            .ToListAsync(ct);

        return MapDetail(cashFlow, journalEntries);
    }

    public Task<CashFlowDetailDto> CreateIncomeAsync(CreateBusinessIncomeRequest request, CancellationToken ct = default)
    {
        return CreateAsync(
            trxType: CashFlowType.In,
            request.TrxDate,
            request.OutletId,
            request.FromChartOfAccountId,
            request.ToChartOfAccountId,
            request.Amount,
            request.Note,
            request.AttachmentUrl,
            ct);
    }

    public Task<CashFlowDetailDto> CreateOutcomeAsync(CreateBusinessOutcomeRequest request, CancellationToken ct = default)
    {
        return CreateAsync(
            trxType: CashFlowType.Out,
            request.TrxDate,
            request.OutletId,
            request.FromChartOfAccountId,
            request.ToChartOfAccountId,
            request.Amount,
            request.Note,
            request.AttachmentUrl,
            ct);
    }

    private async Task<CashFlowDetailDto> CreateAsync(
        string trxType,
        DateTime trxDate,
        Guid? outletId,
        Guid fromChartOfAccountId,
        Guid toChartOfAccountId,
        decimal amount,
        string? note,
        string? attachmentUrl,
        CancellationToken ct)
    {
        var businessId = EnsureBusinessContext();
        var userId = EnsureUserContext();

        if (outletId.HasValue)
        {
            await EnsureOutletAccessibleAsync(outletId.Value, ct);
        }

        var fromAccount = await GetValidatedAccountAsync(fromChartOfAccountId, outletId, ct);
        var toAccount = await GetValidatedAccountAsync(toChartOfAccountId, outletId, ct);

        if (fromAccount.Id == toAccount.Id)
        {
            throw new InvalidOperationException("Akun asal dan tujuan tidak boleh sama.");
        }

        if (!fromAccount.IsCashBank && !toAccount.IsCashBank)
        {
            throw new InvalidOperationException("Salah satu akun harus bertipe kas/bank.");
        }

        var normalizedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        var normalizedAttachmentUrl = string.IsNullOrWhiteSpace(attachmentUrl) ? null : attachmentUrl.Trim();
        var trxDateUtc = DateTime.SpecifyKind(trxDate, DateTimeKind.Utc);
        var trxNumber = await GenerateTransactionNumberAsync(businessId, trxType, trxDateUtc, ct);

        var cashFlow = new CashFlow
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            OutletId = outletId,
            TrxNumber = trxNumber,
            TrxDate = trxDateUtc,
            TrxType = trxType,
            TrxEntity = AccountingTransactionEntity.Business,
            FromChartOfAccountId = fromAccount.Id,
            FromChartOfAccount = fromAccount,
            ToChartOfAccountId = toAccount.Id,
            ToChartOfAccount = toAccount,
            Amount = amount,
            Note = normalizedNote,
            AttachmentUrl = normalizedAttachmentUrl,
            CreatedBy = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var canUseTransaction = _dbContext.Database.IsRelational();
        if (canUseTransaction)
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            _dbContext.CashFlows.Add(cashFlow);
            await _dbContext.SaveChangesAsync(ct);
            await _postingService.PostAsync(cashFlow, fromAccount, toAccount, ct);
            await transaction.CommitAsync(ct);
            return await GetByIdAsync(cashFlow.Id, ct);
        }

        _dbContext.CashFlows.Add(cashFlow);
        await _dbContext.SaveChangesAsync(ct);
        await _postingService.PostAsync(cashFlow, fromAccount, toAccount, ct);
        return await GetByIdAsync(cashFlow.Id, ct);
    }

    private async Task<ChartOfAccount> GetValidatedAccountAsync(Guid accountId, Guid? outletId, CancellationToken ct)
    {
        var account = await _dbContext.ChartOfAccounts
            .Include(current => current.Outlet)
            .FirstOrDefaultAsync(current => current.Id == accountId, ct);

        if (account == null)
        {
            throw new InvalidOperationException("Akun tidak ditemukan.");
        }

        if (!account.IsActive)
        {
            throw new InvalidOperationException($"Akun {account.AccountCode} tidak aktif.");
        }

        if (outletId.HasValue)
        {
            if (account.OutletId.HasValue && account.OutletId != outletId.Value)
            {
                throw new InvalidOperationException($"Akun {account.AccountCode} tidak dapat digunakan untuk outlet ini.");
            }
        }
        else if (account.OutletId.HasValue)
        {
            throw new InvalidOperationException($"Akun {account.AccountCode} hanya dapat digunakan untuk outlet tertentu.");
        }

        return account;
    }

    private async Task<string> GenerateTransactionNumberAsync(Guid businessId, string trxType, DateTime trxDateUtc, CancellationToken ct)
    {
        var prefix = trxType == CashFlowType.In ? "CFI" : "CFO";
        var datePart = trxDateUtc.ToString("yyyyMMdd");
        var trxNumberPrefix = $"{prefix}-{datePart}-";

        var lastSequence = await _dbContext.CashFlows
            .AsNoTracking()
            .Where(cashFlow => cashFlow.BusinessId == businessId)
            .Where(cashFlow => cashFlow.TrxType == trxType)
            .Where(cashFlow => cashFlow.TrxDate.Date == trxDateUtc.Date)
            .Where(cashFlow => cashFlow.TrxNumber.StartsWith(trxNumberPrefix))
            .Select(cashFlow => cashFlow.TrxNumber)
            .ToListAsync(ct);

        var nextSequence = lastSequence
            .Select(value => value.Split('-').LastOrDefault())
            .Select(value => int.TryParse(value, out var sequence) ? sequence : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        return $"{trxNumberPrefix}{nextSequence:0000}";
    }

    private async Task<Outlet> EnsureOutletAccessibleAsync(Guid outletId, CancellationToken ct)
    {
        var outlet = await _dbContext.Outlets
            .AsNoTracking()
            .FirstOrDefaultAsync(current => current.Id == outletId, ct);

        if (outlet == null)
        {
            throw new InvalidOperationException("Outlet tidak ditemukan.");
        }

        if (!outlet.IsActive)
        {
            throw new InvalidOperationException("Outlet tidak aktif.");
        }

        var role = _currentUserService.Role;
        if (role is not "Owner" and not "Admin" and not "Keuangan" && _currentUserService.OutletId != outletId)
        {
            throw new UnauthorizedAccessException("Anda tidak memiliki akses ke outlet tersebut.");
        }

        return outlet;
    }

    private Guid EnsureBusinessContext()
    {
        if (!_currentUserService.BusinessId.HasValue)
        {
            throw new UnauthorizedAccessException("Business context tidak ditemukan.");
        }

        return _currentUserService.BusinessId.Value;
    }

    private Guid EnsureUserContext()
    {
        if (!_currentUserService.UserId.HasValue)
        {
            throw new UnauthorizedAccessException("User context tidak ditemukan.");
        }

        return _currentUserService.UserId.Value;
    }

    private static CashFlowListItemDto MapListItem(CashFlow cashFlow)
    {
        return new CashFlowListItemDto(
            cashFlow.Id,
            cashFlow.TrxNumber,
            cashFlow.TrxDate,
            cashFlow.TrxType,
            cashFlow.TrxEntity,
            cashFlow.Amount,
            cashFlow.FromChartOfAccountId ?? Guid.Empty,
            cashFlow.FromChartOfAccount?.AccountCode ?? string.Empty,
            cashFlow.FromChartOfAccount?.AccountName ?? string.Empty,
            cashFlow.ToChartOfAccountId ?? Guid.Empty,
            cashFlow.ToChartOfAccount?.AccountCode ?? string.Empty,
            cashFlow.ToChartOfAccount?.AccountName ?? string.Empty,
            cashFlow.OutletId,
            cashFlow.Outlet?.Name,
            cashFlow.Note,
            cashFlow.AttachmentUrl,
            cashFlow.CreatedBy,
            cashFlow.CreatedByUser?.Name ?? string.Empty,
            cashFlow.CreatedAt
        );
    }

    private static CashFlowDetailDto MapDetail(CashFlow cashFlow, IReadOnlyList<CashFlowJournalEntryDto> journalEntries)
    {
        return new CashFlowDetailDto(
            cashFlow.Id,
            cashFlow.TrxNumber,
            cashFlow.TrxDate,
            cashFlow.TrxType,
            cashFlow.TrxEntity,
            cashFlow.Amount,
            cashFlow.FromChartOfAccountId ?? Guid.Empty,
            cashFlow.FromChartOfAccount?.AccountCode ?? string.Empty,
            cashFlow.FromChartOfAccount?.AccountName ?? string.Empty,
            cashFlow.ToChartOfAccountId ?? Guid.Empty,
            cashFlow.ToChartOfAccount?.AccountCode ?? string.Empty,
            cashFlow.ToChartOfAccount?.AccountName ?? string.Empty,
            cashFlow.OutletId,
            cashFlow.Outlet?.Name,
            cashFlow.Note,
            cashFlow.AttachmentUrl,
            cashFlow.CreatedBy,
            cashFlow.CreatedByUser?.Name ?? string.Empty,
            cashFlow.CreatedAt,
            journalEntries
        );
    }

    private static CashFlowDetailDto MapDetail(CashFlow cashFlow, IReadOnlyList<AccountTransaction> journalEntries)
    {
        return MapDetail(
            cashFlow,
            journalEntries.Select(entry => new CashFlowJournalEntryDto(
                entry.Id,
                entry.ChartOfAccountId,
                entry.ChartOfAccount?.AccountCode ?? string.Empty,
                entry.ChartOfAccount?.AccountName ?? string.Empty,
                entry.DebitAmount,
                entry.CreditAmount)).ToList());
    }
}
