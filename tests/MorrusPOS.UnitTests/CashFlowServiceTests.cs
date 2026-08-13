using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Accounting;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;
using Xunit;

namespace MorrusPOS.UnitTests;

public class CashFlowServiceTests
{
    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _otherBusinessId = Guid.NewGuid();
    private readonly Guid _outletId = Guid.NewGuid();
    private readonly Guid _otherOutletId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly AppDbContext _dbContext;
    private readonly TestCurrentUserService _currentUser;

    public CashFlowServiceTests()
    {
        _currentUser = new TestCurrentUserService
        {
            BusinessId = _businessId,
            OutletId = null,
            Role = "Admin",
            UserId = _userId,
        };

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AppDbContext(options, _currentUser);

        _dbContext.Businesses.AddRange(
            new Business { Id = _businessId, Name = "Biz A", Category = "Retail" },
            new Business { Id = _otherBusinessId, Name = "Biz B", Category = "Retail" });
        _dbContext.Outlets.AddRange(
            new Outlet
            {
                Id = _outletId,
                BusinessId = _businessId,
                Code = "OUT01",
                Name = "Outlet A",
                IsActive = true,
            },
            new Outlet
            {
                Id = _otherOutletId,
                BusinessId = _businessId,
                Code = "OUT02",
                Name = "Outlet B",
                IsActive = true,
            });
        _dbContext.Users.Add(new User
        {
            Id = _userId,
            BusinessId = _businessId,
            OutletId = null,
            RoleId = Guid.NewGuid(),
            Name = "Finance Admin",
            Email = "finance@example.com",
            PasswordHash = "hash",
            IsActive = true,
        });
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task CreateIncomeAsync_Should_CreateCashFlowAndBalancedJournal_When_BusinessLevelAccountsValid()
    {
        var cashAccount = AddAccount("1010", "Kas Utama", ChartOfAccountType.Asset, true);
        var revenueAccount = AddAccount("4100", "Pendapatan Toko", ChartOfAccountType.Revenue, false);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        var result = await service.CreateIncomeAsync(new CreateBusinessIncomeRequest(
            TrxDate: new DateTime(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc),
            OutletId: null,
            FromChartOfAccountId: cashAccount.Id,
            ToChartOfAccountId: revenueAccount.Id,
            Amount: 150000,
            Note: "Setoran kas",
            AttachmentUrl: null));

        result.TrxNumber.Should().StartWith("CFI-20260813-");
        result.JournalEntries.Should().HaveCount(2);
        result.JournalEntries.Should().ContainSingle(entry =>
            entry.ChartOfAccountId == cashAccount.Id && entry.DebitAmount == 150000 && entry.CreditAmount == 0);
        result.JournalEntries.Should().ContainSingle(entry =>
            entry.ChartOfAccountId == revenueAccount.Id && entry.CreditAmount == 150000 && entry.DebitAmount == 0);
        _dbContext.CashFlows.Should().HaveCount(1);
        _dbContext.AccountTransactions.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateOutcomeAsync_Should_CreateOutletCashFlow_When_OutletScopedAccountsValid()
    {
        var cashAccount = AddAccount("1110", "Kas Outlet A", ChartOfAccountType.Asset, true, _outletId);
        var expenseAccount = AddAccount("6100", "Biaya Outlet A", ChartOfAccountType.Expense, false, _outletId);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        var result = await service.CreateOutcomeAsync(new CreateBusinessOutcomeRequest(
            TrxDate: new DateTime(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc),
            OutletId: _outletId,
            FromChartOfAccountId: cashAccount.Id,
            ToChartOfAccountId: expenseAccount.Id,
            Amount: 50000,
            Note: null,
            AttachmentUrl: null));

        result.OutletId.Should().Be(_outletId);
        result.TrxNumber.Should().StartWith("CFO-20260813-");
        result.JournalEntries.Should().ContainSingle(entry =>
            entry.ChartOfAccountId == cashAccount.Id && entry.CreditAmount == 50000 && entry.DebitAmount == 0);
        result.JournalEntries.Should().ContainSingle(entry =>
            entry.ChartOfAccountId == expenseAccount.Id && entry.DebitAmount == 50000 && entry.CreditAmount == 0);
    }

    [Fact]
    public async Task CreateIncomeAsync_Should_Reject_When_NoCashBankAccountSelected()
    {
        var revenueAccount = AddAccount("4100", "Pendapatan Toko", ChartOfAccountType.Revenue, false);
        var equityAccount = AddAccount("3100", "Modal", ChartOfAccountType.Equity, false);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        var act = () => service.CreateIncomeAsync(new CreateBusinessIncomeRequest(
            TrxDate: DateTime.UtcNow,
            OutletId: null,
            FromChartOfAccountId: revenueAccount.Id,
            ToChartOfAccountId: equityAccount.Id,
            Amount: 10000,
            Note: null,
            AttachmentUrl: null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Salah satu akun harus bertipe kas/bank.");
    }

    [Fact]
    public async Task CreateOutcomeAsync_Should_Reject_When_OutletNullButAccountIsOutletScoped()
    {
        var outletCashAccount = AddAccount("1110", "Kas Outlet A", ChartOfAccountType.Asset, true, _outletId);
        var expenseAccount = AddAccount("6100", "Biaya Umum", ChartOfAccountType.Expense, false);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        var act = () => service.CreateOutcomeAsync(new CreateBusinessOutcomeRequest(
            TrxDate: DateTime.UtcNow,
            OutletId: null,
            FromChartOfAccountId: outletCashAccount.Id,
            ToChartOfAccountId: expenseAccount.Id,
            Amount: 45000,
            Note: null,
            AttachmentUrl: null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Akun {outletCashAccount.AccountCode} hanya dapat digunakan untuk outlet tertentu.");
    }

    [Fact]
    public async Task CreateOutcomeAsync_Should_Reject_When_AccountBelongsToAnotherOutlet()
    {
        var cashAccount = AddAccount("1110", "Kas Outlet A", ChartOfAccountType.Asset, true, _outletId);
        var otherOutletExpense = AddAccount("6101", "Biaya Outlet B", ChartOfAccountType.Expense, false, _otherOutletId);
        await _dbContext.SaveChangesAsync();

        var service = CreateService();
        var act = () => service.CreateOutcomeAsync(new CreateBusinessOutcomeRequest(
            TrxDate: DateTime.UtcNow,
            OutletId: _outletId,
            FromChartOfAccountId: cashAccount.Id,
            ToChartOfAccountId: otherOutletExpense.Id,
            Amount: 45000,
            Note: null,
            AttachmentUrl: null));

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage($"Akun {otherOutletExpense.AccountCode} tidak dapat digunakan untuk outlet ini.");
    }

    private CashFlowService CreateService()
    {
        return new CashFlowService(_dbContext, _currentUser, new CashFlowPostingService(_dbContext));
    }

    private ChartOfAccount AddAccount(
        string code,
        string name,
        string type,
        bool isCashBank,
        Guid? outletId = null,
        bool isActive = true)
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
            IsActive = isActive,
        };

        _dbContext.ChartOfAccounts.Add(account);
        return account;
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
