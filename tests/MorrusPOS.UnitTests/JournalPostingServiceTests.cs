using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Exceptions;
using MorrusPOS.Application.Features.Accounting;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;
using Xunit;

namespace MorrusPOS.UnitTests;

public class JournalPostingServiceTests
{
    private readonly AppDbContext _db;
    private readonly Guid _debitId = Guid.NewGuid();
    private readonly Guid _creditId = Guid.NewGuid();

    public JournalPostingServiceTests()
    {
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, TestTenant.CurrentUser);
        _db.ChartOfAccounts.AddRange(
            new ChartOfAccount { Id = _debitId, AccountCode = "1010", AccountName = "Cash", AccountType = ChartOfAccountType.Asset, IsCashBank = true, IsActive = true },
            new ChartOfAccount { Id = _creditId, AccountCode = "4100", AccountName = "Revenue", AccountType = ChartOfAccountType.Revenue, IsActive = true });
        _db.SaveChanges();
    }

    [Fact]
    public async Task PostAsync_Should_CreateBalancedJournal_AndReplaySameDraft()
    {
        var service = new JournalPostingService(_db);
        var referenceId = Guid.NewGuid();
        var draft = Draft(referenceId, 100);

        var first = await service.PostAsync(draft);
        var replay = await service.PostAsync(draft);

        first.Created.Should().BeTrue();
        replay.Created.Should().BeFalse();
        replay.JournalId.Should().Be(first.JournalId);
        (await _db.JournalEntries.CountAsync()).Should().Be(1);
        var lines = await _db.AccountTransactions.Where(x => x.JournalEntryId == first.JournalId).ToListAsync();
        lines.Should().HaveCount(2);
        lines.Sum(x => x.DebitAmount).Should().Be(lines.Sum(x => x.CreditAmount));
    }

    [Fact]
    public async Task PostAsync_Should_RejectUnbalancedAndConflictingDrafts()
    {
        var service = new JournalPostingService(_db);
        var referenceId = Guid.NewGuid();
        var unbalanced = Draft(referenceId, 100) with
        {
            Lines = [new JournalDraftLine(_debitId, 100, 0), new JournalDraftLine(_creditId, 0, 90)]
        };
        await FluentActions.Invoking(() => service.PostAsync(unbalanced))
            .Should().ThrowAsync<AccountingIntegrityException>()
            .Where(x => x.Code == AccountingIntegrityErrorCodes.JournalUnbalanced);

        await service.PostAsync(Draft(referenceId, 100));
        await FluentActions.Invoking(() => service.PostAsync(Draft(referenceId, 200)))
            .Should().ThrowAsync<AccountingIntegrityException>()
            .Where(x => x.Code == AccountingIntegrityErrorCodes.JournalPostingConflict);
    }

    [Fact]
    public async Task ReverseAsync_Should_SwapDebitAndCredit()
    {
        var service = new JournalPostingService(_db);
        var original = await service.PostAsync(Draft(Guid.NewGuid(), 125));

        var reversal = await service.ReverseAsync(original.JournalId, "transaction_void",
            Guid.NewGuid(), "VOID-1", "Void", TestTenant.CurrentUser.UserId);
        var lines = await _db.AccountTransactions.Where(x => x.JournalEntryId == reversal.JournalId).ToListAsync();

        lines.Should().ContainSingle(x => x.ChartOfAccountId == _debitId && x.CreditAmount == 125);
        lines.Should().ContainSingle(x => x.ChartOfAccountId == _creditId && x.DebitAmount == 125);
    }

    private JournalDraft Draft(Guid referenceId, decimal amount) => new(
        TestTenant.BusinessId, null, DateTime.UtcNow, "JRN-1", "test", referenceId, "Test",
        [new JournalDraftLine(_debitId, amount, 0), new JournalDraftLine(_creditId, 0, amount)],
        CreatedBy: TestTenant.CurrentUser.UserId);
}
