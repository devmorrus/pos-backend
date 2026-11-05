using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public static class DefaultAccountingSetup
{
    private sealed record DefaultAccount(
        string Code,
        string Name,
        string Type,
        string Role,
        bool IsCashBank = false);

    private static readonly DefaultAccount[] Accounts =
    [
        new("1010", "Kas", ChartOfAccountType.Asset, AccountingAccountRole.Cash, true),
        new("1020", "Bank", ChartOfAccountType.Asset, AccountingAccountRole.Bank, true),
        new("1100", "Piutang Usaha", ChartOfAccountType.Asset, AccountingAccountRole.AccountsReceivable),
        new("1200", "Persediaan Barang", ChartOfAccountType.Asset, AccountingAccountRole.Inventory),
        new("1300", "Clearing Channel", ChartOfAccountType.Asset, AccountingAccountRole.ChannelClearing),
        new("2990", "Exchange Clearing", ChartOfAccountType.Liability, AccountingAccountRole.ExchangeClearing),
        new("2100", "Utang Usaha", ChartOfAccountType.Liability, AccountingAccountRole.AccountsPayable),
        new("2200", "Utang Pajak", ChartOfAccountType.Liability, AccountingAccountRole.TaxPayable),
        new("2300", "Utang Konsinyasi", ChartOfAccountType.Liability, AccountingAccountRole.ConsignmentPayable),
        new("4100", "Pendapatan Penjualan", ChartOfAccountType.Revenue, AccountingAccountRole.SalesRevenue),
        new("4110", "Pendapatan Service Charge", ChartOfAccountType.Revenue, AccountingAccountRole.ServiceChargeRevenue),
        new("4190", "Retur Penjualan", ChartOfAccountType.Revenue, AccountingAccountRole.SalesReturns),
        new("5100", "Harga Pokok Penjualan", ChartOfAccountType.Cogs, AccountingAccountRole.Cogs),
        new("6100", "Biaya Channel", ChartOfAccountType.Expense, AccountingAccountRole.ChannelFeeExpense),
        new("6110", "Biaya Petty Cash", ChartOfAccountType.Expense, AccountingAccountRole.PettyCashExpense),
        new("6200", "Selisih Kurang Stok", ChartOfAccountType.Expense, AccountingAccountRole.InventoryLoss),
        new("7100", "Selisih Lebih Stok", ChartOfAccountType.Revenue, AccountingAccountRole.InventoryGain),
    ];

    public static void AddDefaults(AppDbContext dbContext, Guid businessId)
    {
        var now = DateTime.UtcNow;
        foreach (var item in Accounts)
        {
            var account = new ChartOfAccount
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                AccountCode = item.Code,
                AccountName = item.Name,
                AccountType = item.Type,
                IsCashBank = item.IsCashBank,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
            };

            dbContext.ChartOfAccounts.Add(account);
            dbContext.AccountingAccountMappings.Add(new AccountingAccountMapping
            {
                Id = Guid.NewGuid(),
                BusinessId = businessId,
                OutletId = null,
                Role = item.Role,
                ChartOfAccountId = account.Id,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
    }
}
