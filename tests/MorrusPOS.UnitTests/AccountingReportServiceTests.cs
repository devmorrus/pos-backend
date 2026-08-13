using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Reports;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;
using Xunit;

namespace MorrusPOS.UnitTests;

public class AccountingReportServiceTests
{
    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _outletAId = Guid.NewGuid();
    private readonly Guid _outletBId = Guid.NewGuid();
    private readonly AppDbContext _dbContext;
    private readonly TestCurrentUserService _currentUser;

    public AccountingReportServiceTests()
    {
        _currentUser = new TestCurrentUserService
        {
            BusinessId = _businessId,
            OutletId = null,
            Role = "Admin",
            UserId = Guid.NewGuid(),
        };

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options, _currentUser);

        _dbContext.Businesses.Add(new Business
        {
            Id = _businessId,
            Name = "Biz A",
            Category = "Retail",
        });
        _dbContext.Outlets.AddRange(
            new Outlet
            {
                Id = _outletAId,
                BusinessId = _businessId,
                Code = "OUT-A",
                Name = "Outlet A",
                IsActive = true,
            },
            new Outlet
            {
                Id = _outletBId,
                BusinessId = _businessId,
                Code = "OUT-B",
                Name = "Outlet B",
                IsActive = true,
            });
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task GetCashFlowReportAsync_Should_CalculateOpeningSummaryAndRunningBalance()
    {
        var cashMain = AddAccount("1010", "Kas Utama", ChartOfAccountType.Asset, true);
        var pettyCash = AddAccount("1011", "Kas Kecil", ChartOfAccountType.Asset, true);
        var inventory = AddAccount("1200", "Persediaan", ChartOfAccountType.Asset, false);
        AddAccountTransaction(cashMain, new DateTime(2026, 7, 31, 9, 0, 0, DateTimeKind.Utc), "CFI-20260731-0001", debit: 1000, credit: 0, note: "Saldo awal");
        AddAccountTransaction(cashMain, new DateTime(2026, 8, 2, 9, 0, 0, DateTimeKind.Utc), "CFI-20260802-0001", debit: 500, credit: 0, note: "Pemasukan");
        AddAccountTransaction(cashMain, new DateTime(2026, 8, 3, 10, 0, 0, DateTimeKind.Utc), "CFO-20260803-0001", debit: 0, credit: 200, note: "Pengeluaran");
        AddAccountTransaction(pettyCash, new DateTime(2026, 8, 4, 11, 0, 0, DateTimeKind.Utc), "CFI-20260804-0001", debit: 100, credit: 0, note: "Kas kecil");
        AddAccountTransaction(inventory, new DateTime(2026, 8, 5, 9, 0, 0, DateTimeKind.Utc), "INV-20260805-0001", debit: 999, credit: 0, note: "Bukan kas");
        await _dbContext.SaveChangesAsync();

        var service = new ReportService(_dbContext, _currentUser);
        var report = await service.GetCashFlowReportAsync(new AccountingCashFlowReportFilters(
            DateFrom: new DateTime(2026, 8, 1),
            DateTo: new DateTime(2026, 8, 31),
            OutletId: null,
            ChartOfAccountId: null,
            Keyword: null));

        report.Summary.OpeningBalance.Should().Be(1000);
        report.Summary.CashIn.Should().Be(600);
        report.Summary.CashOut.Should().Be(200);
        report.Summary.ClosingBalance.Should().Be(1400);
        report.Lines.Should().HaveCount(3);
        report.Lines.Last().RunningBalance.Should().Be(1400);
        report.Lines.Should().OnlyContain(line => line.AccountCode != inventory.AccountCode);
    }

    [Fact]
    public async Task GetCashFlowReportAsync_Should_FilterByOutletAndKeyword()
    {
        var cashA = AddAccount("1010", "Kas Outlet A", ChartOfAccountType.Asset, true, _outletAId);
        var cashB = AddAccount("1011", "Kas Outlet B", ChartOfAccountType.Asset, true, _outletBId);
        AddAccountTransaction(cashA, new DateTime(2026, 8, 10, 9, 0, 0, DateTimeKind.Utc), "CFI-20260810-0001", debit: 100, credit: 0, outletId: _outletAId, note: "Alpha");
        AddAccountTransaction(cashB, new DateTime(2026, 8, 10, 10, 0, 0, DateTimeKind.Utc), "CFI-20260810-0002", debit: 200, credit: 0, outletId: _outletBId, note: "Bravo");
        await _dbContext.SaveChangesAsync();

        var service = new ReportService(_dbContext, _currentUser);
        var report = await service.GetCashFlowReportAsync(new AccountingCashFlowReportFilters(
            DateFrom: new DateTime(2026, 8, 1),
            DateTo: new DateTime(2026, 8, 31),
            OutletId: _outletAId,
            ChartOfAccountId: null,
            Keyword: "Alpha"));

        report.Lines.Should().HaveCount(1);
        report.Lines[0].OutletId.Should().Be(_outletAId);
        report.Lines[0].Note.Should().Contain("Alpha");
    }

    [Fact]
    public async Task GetAccountingProfitLossReportAsync_Should_GroupByAccountTypeAndCalculateSummary()
    {
        var revenue = AddAccount("4100", "Pendapatan Toko", ChartOfAccountType.Revenue, false);
        var cogs = AddAccount("5100", "HPP Toko", ChartOfAccountType.Cogs, false);
        var expense = AddAccount("6100", "Biaya Operasional", ChartOfAccountType.Expense, false);
        var asset = AddAccount("1010", "Kas Utama", ChartOfAccountType.Asset, true);

        AddAccountTransaction(revenue, new DateTime(2026, 8, 5, 9, 0, 0, DateTimeKind.Utc), "REV-1", debit: 0, credit: 1000, note: "Penjualan");
        AddAccountTransaction(cogs, new DateTime(2026, 8, 5, 9, 1, 0, DateTimeKind.Utc), "COGS-1", debit: 400, credit: 0, note: "HPP");
        AddAccountTransaction(expense, new DateTime(2026, 8, 6, 9, 1, 0, DateTimeKind.Utc), "EXP-1", debit: 150, credit: 0, note: "Listrik");
        AddAccountTransaction(asset, new DateTime(2026, 8, 6, 9, 2, 0, DateTimeKind.Utc), "AST-1", debit: 700, credit: 0, note: "Kas");
        await _dbContext.SaveChangesAsync();

        var service = new ReportService(_dbContext, _currentUser);
        var report = await service.GetAccountingProfitLossReportAsync(new AccountingProfitLossReportFilters(
            DateFrom: new DateTime(2026, 8, 1),
            DateTo: new DateTime(2026, 8, 31),
            OutletId: null,
            Keyword: null));

        report.Summary.RevenueTotal.Should().Be(1000);
        report.Summary.CogsTotal.Should().Be(400);
        report.Summary.ExpenseTotal.Should().Be(150);
        report.Summary.GrossProfit.Should().Be(600);
        report.Summary.NetProfit.Should().Be(450);
        report.Revenue.Accounts.Should().HaveCount(1);
        report.Cogs.Accounts.Should().HaveCount(1);
        report.Expense.Accounts.Should().HaveCount(1);
        report.Revenue.Accounts[0].AccountCode.Should().Be("4100");
    }

    [Fact]
    public async Task GetAccountingProfitLossReportAsync_Should_ReturnZeroSummary_When_NoData()
    {
        var service = new ReportService(_dbContext, _currentUser);
        var report = await service.GetAccountingProfitLossReportAsync(new AccountingProfitLossReportFilters(
            DateFrom: new DateTime(2026, 8, 1),
            DateTo: new DateTime(2026, 8, 31),
            OutletId: null,
            Keyword: null));

        report.Summary.RevenueTotal.Should().Be(0);
        report.Summary.CogsTotal.Should().Be(0);
        report.Summary.ExpenseTotal.Should().Be(0);
        report.Summary.NetProfit.Should().Be(0);
    }

    private ChartOfAccount AddAccount(string code, string name, string type, bool isCashBank, Guid? outletId = null)
    {
        var account = new ChartOfAccount
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            OutletId = outletId,
            AccountCode = code,
            AccountName = name,
            AccountType = type,
            IsCashBank = isCashBank,
            IsActive = true,
        };

        _dbContext.ChartOfAccounts.Add(account);
        return account;
    }

    private void AddAccountTransaction(
        ChartOfAccount account,
        DateTime trxDate,
        string trxNumber,
        decimal debit,
        decimal credit,
        Guid? outletId = null,
        string? note = null)
    {
        _dbContext.AccountTransactions.Add(new AccountTransaction
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            OutletId = outletId,
            TrxDate = trxDate,
            TrxNumber = trxNumber,
            ReferenceType = "cash_flow",
            ReferenceId = Guid.NewGuid(),
            TrxEntity = AccountingTransactionEntity.Business,
            ChartOfAccountId = account.Id,
            ChartOfAccount = account,
            DebitAmount = debit,
            CreditAmount = credit,
            Note = note,
            CreatedAt = trxDate,
            UpdatedAt = trxDate,
        });
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; set; }
        public Guid? OutletId { get; set; }
        public Guid? BusinessId { get; set; }
        public string? Role { get; set; }
        public bool IsAuthenticated => true;
    }
}
