using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MorrusPOS.Application.Common.Exceptions;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Accounting;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Options;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public static class AccountingRoleFallback
{
    public sealed record Rule(string AccountType, string[] Keywords, bool RequireCashBank = false, bool ExcludeCashBank = false);

    public static readonly IReadOnlyDictionary<string, Rule> Rules = new Dictionary<string, Rule>
    {
        [AccountingAccountRole.Cash] = new(ChartOfAccountType.Asset, ["kas", "cash"], RequireCashBank: true),
        [AccountingAccountRole.Bank] = new(ChartOfAccountType.Asset, ["bank", "transfer", "giro"], RequireCashBank: true),
        [AccountingAccountRole.AccountsReceivable] = new(ChartOfAccountType.Asset, ["piutang", "receivable"], ExcludeCashBank: true),
        [AccountingAccountRole.ChannelClearing] = new(ChartOfAccountType.Asset, ["clearing", "channel", "settlement", "piutang"], ExcludeCashBank: true),
        [AccountingAccountRole.Inventory] = new(ChartOfAccountType.Asset, ["persediaan", "inventory", "stok", "stock"], ExcludeCashBank: true),
        [AccountingAccountRole.SalesRevenue] = new(ChartOfAccountType.Revenue, ["penjualan", "sales", "pendapatan"]),
        [AccountingAccountRole.SalesReturns] = new(ChartOfAccountType.Revenue, ["retur penjualan", "sales return", "refund"]),
        [AccountingAccountRole.ServiceChargeRevenue] = new(ChartOfAccountType.Revenue, ["service", "layanan"]),
        [AccountingAccountRole.TaxPayable] = new(ChartOfAccountType.Liability, ["pajak", "tax"]),
        [AccountingAccountRole.Cogs] = new(ChartOfAccountType.Cogs, []),
        [AccountingAccountRole.AccountsPayable] = new(ChartOfAccountType.Liability, ["utang", "hutang", "payable", "supplier"]),
        [AccountingAccountRole.ConsignmentPayable] = new(ChartOfAccountType.Liability, ["konsinyasi", "consignment", "titipan"]),
        [AccountingAccountRole.ChannelFeeExpense] = new(ChartOfAccountType.Expense, ["fee", "komisi", "commission", "admin", "channel"]),
        [AccountingAccountRole.InventoryGain] = new(ChartOfAccountType.Revenue, ["selisih lebih", "inventory gain", "stock gain"]),
        [AccountingAccountRole.InventoryLoss] = new(ChartOfAccountType.Expense, ["selisih kurang", "inventory loss", "stock loss"]),
        [AccountingAccountRole.PettyCashExpense] = new(ChartOfAccountType.Expense, ["petty", "operasional", "biaya"]),
        [AccountingAccountRole.ExchangeClearing] = new(ChartOfAccountType.Liability, ["exchange", "clearing"]),
    };

    public static ChartOfAccount? FindFallback(
        IEnumerable<ChartOfAccount> accounts, string role, Guid? outletId)
    {
        if (!Rules.TryGetValue(role, out var rule)) return null;

        var candidates = accounts
            .Where(x => x.AccountType == rule.AccountType && x.IsActive)
            .Where(x => !rule.RequireCashBank || x.IsCashBank)
            .Where(x => !rule.ExcludeCashBank || !x.IsCashBank)
            .Where(x => x.OutletId == null || x.OutletId == outletId)
            .ToList();
        if (candidates.Count == 0) return null;

        var keywords = rule.Keywords;
        return candidates
            .OrderByDescending(x => x.OutletId == outletId)
            .ThenByDescending(x => keywords.Count(keyword =>
                $"{x.AccountCode} {x.AccountName}".Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .ThenBy(x => x.AccountCode)
            .ThenBy(x => x.AccountName)
            .First();
    }
}

public class AccountingAccountMappingService : IAccountingAccountMappingService
{
    private readonly AppDbContext _dbContext;
    private readonly ICurrentUserService _currentUserService;
    private readonly AccountingOptions _options;
    private readonly ILogger<AccountingAccountMappingService>? _logger;

    public AccountingAccountMappingService(
        AppDbContext dbContext,
        ICurrentUserService currentUserService,
        IOptions<AccountingOptions>? options = null,
        ILogger<AccountingAccountMappingService>? logger = null)
    {
        _dbContext = dbContext;
        _currentUserService = currentUserService;
        _options = options?.Value ?? new AccountingOptions();
        _logger = logger;
    }

    public async Task<ChartOfAccount> ResolveAsync(
        Guid businessId, Guid? outletId, string role, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(role) || !AccountingAccountRole.All.Contains(role))
            throw new AccountingIntegrityException(AccountingIntegrityErrorCodes.AccountMappingMissing,
                $"Accounting role '{role}' tidak dikenal.");

        var mappings = await _dbContext.AccountingAccountMappings
            .AsNoTracking()
            .Include(x => x.ChartOfAccount)
            .Where(x => x.BusinessId == businessId && x.Role == role)
            .Where(x => x.OutletId == null || x.OutletId == outletId)
            .OrderByDescending(x => x.OutletId == outletId)
            .ToListAsync(ct);

        var mapping = mappings.FirstOrDefault();
        if (mapping is not null)
        {
            ValidateAccount(mapping.ChartOfAccount, businessId, outletId, role);
            return mapping.ChartOfAccount;
        }

        if (_options.StrictMappings)
            throw new AccountingIntegrityException(AccountingIntegrityErrorCodes.AccountMappingMissing,
                $"Account mapping '{role}' belum dikonfigurasi untuk outlet ini.");

        var accounts = await _dbContext.ChartOfAccounts.AsNoTracking()
            .Where(x => x.BusinessId == businessId && x.IsActive)
            .Where(x => x.OutletId == null || x.OutletId == outletId)
            .ToListAsync(ct);

        var fallback = AccountingRoleFallback.FindFallback(accounts, role, outletId);
        if (fallback is null)
            throw new AccountingIntegrityException(AccountingIntegrityErrorCodes.AccountMappingMissing,
                $"Akun untuk role '{role}' tidak ditemukan. Konfigurasikan account mapping terlebih dahulu.");

        _logger?.LogWarning(
            "AccountMappingFallbackUsed BusinessId={BusinessId} OutletId={OutletId} Role={Role} AccountId={AccountId}",
            businessId, outletId, role, fallback.Id);
        return fallback;
    }

    public async Task<IReadOnlyList<AccountingAccountMappingDto>> GetAsync(
        Guid? outletId, CancellationToken ct = default)
    {
        var businessId = EnsureBusinessContext();
        var mappings = await _dbContext.AccountingAccountMappings
            .AsNoTracking()
            .Include(x => x.ChartOfAccount)
            .Where(x => x.BusinessId == businessId)
            .Where(x => outletId == null || x.OutletId == null || x.OutletId == outletId)
            .OrderBy(x => x.Role)
            .ThenBy(x => x.OutletId)
            .ToListAsync(ct);

        return mappings.Select(Map).ToList();
    }

    public async Task<AccountingAccountMappingDto> UpsertAsync(
        string role, UpsertAccountingAccountMappingRequest request, CancellationToken ct = default)
    {
        var businessId = EnsureBusinessContext();
        if (!AccountingAccountRole.All.Contains(role))
            throw new AccountingIntegrityException(AccountingIntegrityErrorCodes.AccountMappingInvalid,
                $"Accounting role '{role}' tidak dikenal.");

        var account = await _dbContext.ChartOfAccounts
            .FirstOrDefaultAsync(x => x.Id == request.ChartOfAccountId, ct)
            ?? throw new AccountingIntegrityException(AccountingIntegrityErrorCodes.AccountMappingInvalid,
                "Chart of account tidak ditemukan.");
        ValidateAccount(account, businessId, request.OutletId, role);

        var mapping = await _dbContext.AccountingAccountMappings
            .FirstOrDefaultAsync(x => x.BusinessId == businessId && x.Role == role &&
                x.OutletId == request.OutletId, ct);

        if (mapping is null)
        {
            mapping = new AccountingAccountMapping
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                OutletId = request.OutletId,
                Role = role,
                ChartOfAccountId = account.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _dbContext.AccountingAccountMappings.Add(mapping);
        }
        else
        {
            mapping.ChartOfAccountId = account.Id;
            mapping.UpdatedAt = DateTime.UtcNow;
        }

        await _dbContext.SaveChangesAsync(ct);
        mapping.ChartOfAccount = account;
        return Map(mapping);
    }

    private static void ValidateAccount(
        ChartOfAccount account, Guid businessId, Guid? outletId, string role)
    {
        if (!account.IsActive || account.BusinessId != businessId)
            throw new AccountingIntegrityException(AccountingIntegrityErrorCodes.AccountMappingInvalid,
                $"Akun mapping '{role}' tidak aktif atau bukan milik business ini.");

        if (outletId.HasValue)
        {
            if (account.OutletId.HasValue && account.OutletId != outletId)
                throw new AccountingIntegrityException(AccountingIntegrityErrorCodes.AccountMappingInvalid,
                    $"Akun mapping '{role}' bukan milik outlet ini.");
        }
        else if (account.OutletId.HasValue)
        {
            throw new AccountingIntegrityException(AccountingIntegrityErrorCodes.AccountMappingInvalid,
                $"Akun mapping business-level '{role}' tidak boleh memakai akun khusus outlet.");
        }
    }

    private Guid EnsureBusinessContext()
        => _currentUserService.BusinessId
           ?? throw new UnauthorizedAccessException("Business context tidak ditemukan.");

    private static AccountingAccountMappingDto Map(AccountingAccountMapping mapping)
        => new(
            mapping.Id,
            mapping.OutletId,
            mapping.Role,
            mapping.ChartOfAccountId,
            mapping.ChartOfAccount?.AccountCode ?? string.Empty,
            mapping.ChartOfAccount?.AccountName ?? string.Empty,
            mapping.ChartOfAccount?.IsActive ?? false,
            mapping.UpdatedAt);
}
