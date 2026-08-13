using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Accounting;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;
using Xunit;

namespace MorrusPOS.UnitTests;

public class ChartOfAccountServiceTests
{
    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _otherBusinessId = Guid.NewGuid();
    private readonly Guid _outletId = Guid.NewGuid();
    private readonly AppDbContext _dbContext;
    private readonly TestCurrentUserService _currentUser;

    public ChartOfAccountServiceTests()
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

        _dbContext.Businesses.AddRange(
            new Business { Id = _businessId, Name = "Biz A", Category = "Retail" },
            new Business { Id = _otherBusinessId, Name = "Biz B", Category = "Retail" });
        _dbContext.Outlets.Add(new Outlet
        {
            Id = _outletId,
            BusinessId = _businessId,
            Code = "OUT01",
            Name = "Outlet A",
            IsActive = true,
        });
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task CreateAsync_Should_CreateBusinessLevelAssetAccount_When_Valid()
    {
        var service = new ChartOfAccountService(_dbContext, _currentUser);

        var result = await service.CreateAsync(new CreateChartOfAccountRequest(
            AccountCode: "1010",
            AccountName: "Kas Utama",
            AccountType: ChartOfAccountType.Asset,
            IsCashBank: true,
            OutletId: null,
            ParentAccountId: null));

        result.AccountCode.Should().Be("1010");
        result.AccountName.Should().Be("Kas Utama");
        result.IsCashBank.Should().BeTrue();
        result.OutletId.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_Should_CreateOutletLevelExpenseAccount_When_Valid()
    {
        var service = new ChartOfAccountService(_dbContext, _currentUser);

        var result = await service.CreateAsync(new CreateChartOfAccountRequest(
            AccountCode: "6100",
            AccountName: "Biaya Outlet",
            AccountType: ChartOfAccountType.Expense,
            IsCashBank: false,
            OutletId: _outletId,
            ParentAccountId: null));

        result.OutletId.Should().Be(_outletId);
        result.OutletName.Should().Be("Outlet A");
        result.AccountType.Should().Be(ChartOfAccountType.Expense);
    }

    [Fact]
    public async Task CreateAsync_Should_RejectDuplicateCodeWithinSameBusiness()
    {
        _dbContext.ChartOfAccounts.Add(new ChartOfAccount
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            AccountCode = "1010",
            AccountName = "Kas Lama",
            AccountType = ChartOfAccountType.Asset,
        });
        await _dbContext.SaveChangesAsync();

        var service = new ChartOfAccountService(_dbContext, _currentUser);
        var act = () => service.CreateAsync(new CreateChartOfAccountRequest(
            AccountCode: "1010",
            AccountName: "Kas Baru",
            AccountType: ChartOfAccountType.Asset,
            IsCashBank: false,
            OutletId: null,
            ParentAccountId: null));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Kode akun sudah digunakan.");
    }

    [Fact]
    public async Task CreateAsync_Should_AllowSameCodeAcrossDifferentBusiness()
    {
        _dbContext.ChartOfAccounts.Add(new ChartOfAccount
        {
            Id = Guid.NewGuid(),
            BusinessId = _otherBusinessId,
            AccountCode = "1010",
            AccountName = "Kas Biz B",
            AccountType = ChartOfAccountType.Asset,
        });
        await _dbContext.SaveChangesAsync();

        var service = new ChartOfAccountService(_dbContext, _currentUser);
        var result = await service.CreateAsync(new CreateChartOfAccountRequest(
            AccountCode: "1010",
            AccountName: "Kas Biz A",
            AccountType: ChartOfAccountType.Asset,
            IsCashBank: false,
            OutletId: null,
            ParentAccountId: null));

        result.AccountName.Should().Be("Kas Biz A");
    }

    [Fact]
    public async Task CreateAsync_Should_RejectCashBankForNonAsset()
    {
        var service = new ChartOfAccountService(_dbContext, _currentUser);
        var act = () => service.CreateAsync(new CreateChartOfAccountRequest(
            AccountCode: "4100",
            AccountName: "Pendapatan Lain",
            AccountType: ChartOfAccountType.Revenue,
            IsCashBank: true,
            OutletId: null,
            ParentAccountId: null));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Akun kas/bank hanya boleh menggunakan tipe asset.");
    }

    [Fact]
    public async Task CreateAsync_Should_RejectParentWithDifferentType()
    {
        var parent = new ChartOfAccount
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            AccountCode = "1000",
            AccountName = "Aset",
            AccountType = ChartOfAccountType.Asset,
        };
        _dbContext.ChartOfAccounts.Add(parent);
        await _dbContext.SaveChangesAsync();

        var service = new ChartOfAccountService(_dbContext, _currentUser);
        var act = () => service.CreateAsync(new CreateChartOfAccountRequest(
            AccountCode: "6100",
            AccountName: "Biaya Outlet",
            AccountType: ChartOfAccountType.Expense,
            IsCashBank: false,
            OutletId: null,
            ParentAccountId: parent.Id));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Akun induk harus memiliki tipe akun yang sama.");
    }

    [Fact]
    public async Task CreateAsync_Should_RejectBusinessLevelChildOfOutletParent()
    {
        var parent = new ChartOfAccount
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            OutletId = _outletId,
            AccountCode = "6100",
            AccountName = "Biaya Outlet",
            AccountType = ChartOfAccountType.Expense,
        };
        _dbContext.ChartOfAccounts.Add(parent);
        await _dbContext.SaveChangesAsync();

        var service = new ChartOfAccountService(_dbContext, _currentUser);
        var act = () => service.CreateAsync(new CreateChartOfAccountRequest(
            AccountCode: "6110",
            AccountName: "Biaya Outlet Child",
            AccountType: ChartOfAccountType.Expense,
            IsCashBank: false,
            OutletId: null,
            ParentAccountId: parent.Id));

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("Akun global business tidak boleh memakai akun induk khusus outlet.");
    }

    [Fact]
    public async Task UpdateStatusAsync_Should_ToggleStatus()
    {
        var account = new ChartOfAccount
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            AccountCode = "1010",
            AccountName = "Kas Utama",
            AccountType = ChartOfAccountType.Asset,
            IsActive = true,
        };
        _dbContext.ChartOfAccounts.Add(account);
        await _dbContext.SaveChangesAsync();

        var service = new ChartOfAccountService(_dbContext, _currentUser);
        var updated = await service.UpdateStatusAsync(account.Id, new UpdateChartOfAccountStatusRequest(false));

        updated.IsActive.Should().BeFalse();
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
