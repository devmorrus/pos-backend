using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MorrusPOS.Application.Common.Exceptions;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Options;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;
using Xunit;

namespace MorrusPOS.UnitTests;

public class AccountingGovernanceServiceTests
{
    private readonly Guid _businessId = Guid.NewGuid();
    private readonly Guid _outletId = Guid.NewGuid();
    private readonly AppDbContext _db;
    private readonly TestCurrentUserService _currentUser;

    public AccountingGovernanceServiceTests()
    {
        _currentUser = new TestCurrentUserService { BusinessId = _businessId, UserId = Guid.NewGuid() };
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, _currentUser);
        _db.Businesses.Add(new Business { Id = _businessId, Name = "Biz", Category = "Retail" });
        _db.Outlets.Add(new Outlet
        {
            Id = _outletId, BusinessId = _businessId, Code = "OUT", Name = "Outlet", IsActive = true,
        });
        _db.SaveChanges();
    }

    [Fact]
    public async Task ResolveAsync_ShouldPreferExplicitMapping_OverKeywordFallback()
    {
        var fallback = AddAccount("1010", "Kas", ChartOfAccountType.Asset, true);
        var mapped = AddAccount("1020", "Bank", ChartOfAccountType.Asset, true);
        _db.AccountingAccountMappings.Add(new AccountingAccountMapping
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            OutletId = _outletId,
            Role = AccountingAccountRole.Cash,
            ChartOfAccountId = mapped.Id,
        });
        await _db.SaveChangesAsync();

        var service = new AccountingAccountMappingService(_db, _currentUser);
        var resolved = await service.ResolveAsync(_businessId, _outletId, AccountingAccountRole.Cash);

        resolved.Id.Should().Be(mapped.Id);
        fallback.Id.Should().NotBe(mapped.Id);
    }

    [Fact]
    public async Task ResolveAsync_ShouldFailInStrictMode_WhenMappingIsMissing()
    {
        AddAccount("1010", "Kas", ChartOfAccountType.Asset, true);
        await _db.SaveChangesAsync();

        var service = new AccountingAccountMappingService(_db, _currentUser,
            Options.Create(new AccountingOptions { StrictMappings = true }));

        await FluentActions.Invoking(() =>
                service.ResolveAsync(_businessId, _outletId, AccountingAccountRole.Cash))
            .Should().ThrowAsync<AccountingIntegrityException>()
            .Where(x => x.Code == AccountingIntegrityErrorCodes.AccountMappingMissing);
    }

    [Fact]
    public async Task Readiness_ShouldReportMissingMappings_AndUnpostedDocuments()
    {
        _db.Transactions.Add(new Transaction
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            OutletId = _outletId,
            UserId = _currentUser.UserId!.Value,
            TransactionNumber = "TRX-READY",
            Channel = TransactionChannel.Pos,
            Status = TransactionStatus.Completed,
            CustomerType = TransactionCustomerType.Guest,
            GrandTotal = 100,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();

        var service = new AccountingReconciliationService(_db, _currentUser);
        var readiness = await service.GetReadinessAsync();

        readiness.IsReady.Should().BeFalse();
        readiness.MissingMappings.Should().Contain(AccountingAccountRole.Cash);
        readiness.UnpostedDocumentCounts["transaction_sale"].Should().Be(1);
    }

    private ChartOfAccount AddAccount(string code, string name, string type, bool isCashBank)
    {
        var account = new ChartOfAccount
        {
            Id = Guid.NewGuid(),
            BusinessId = _businessId,
            AccountCode = code,
            AccountName = name,
            AccountType = type,
            IsCashBank = isCashBank,
            IsActive = true,
        };
        _db.ChartOfAccounts.Add(account);
        return account;
    }

    private sealed class TestCurrentUserService : ICurrentUserService
    {
        public Guid? UserId { get; set; }
        public Guid? OutletId { get; set; }
        public Guid? BusinessId { get; set; }
        public string? Role { get; set; } = "Owner";
        public bool IsAuthenticated => true;
    }
}
