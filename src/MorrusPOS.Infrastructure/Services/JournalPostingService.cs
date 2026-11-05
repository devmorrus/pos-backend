using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using MorrusPOS.Application.Common.Exceptions;
using MorrusPOS.Application.Features.Accounting;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Infrastructure.Services;

public class JournalPostingService : IJournalPostingService
{
    private readonly AppDbContext _dbContext;
    private readonly ILogger<JournalPostingService>? _logger;

    public JournalPostingService(AppDbContext dbContext, ILogger<JournalPostingService>? logger = null)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<JournalPostingResult> PostAsync(JournalDraft draft, CancellationToken ct = default)
    {
        draft = Normalize(draft);
        ValidateDraft(draft);

        IDbContextTransaction? ownedTransaction = null;
        if (_dbContext.Database.IsRelational() && _dbContext.Database.CurrentTransaction is null)
            ownedTransaction = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            if (_dbContext.Database.IsRelational())
            {
                var key = $"{draft.BusinessId:N}:journal:{draft.ReferenceType}:{draft.ReferenceId:N}";
                await _dbContext.Database.SqlQuery<int>(
                    $"SELECT 1 AS \"Value\" FROM (SELECT pg_advisory_xact_lock(hashtextextended({key},0))) lock_row")
                    .SingleAsync(ct);
            }

            var postingHash = ComputeHash(draft);
            var existing = await _dbContext.JournalEntries.AsNoTracking()
                .SingleOrDefaultAsync(x => x.BusinessId == draft.BusinessId &&
                    x.ReferenceType == draft.ReferenceType && x.ReferenceId == draft.ReferenceId, ct);
            if (existing is not null)
            {
                if (!string.Equals(existing.PostingHash, postingHash, StringComparison.Ordinal))
                    throw new AccountingIntegrityException(AccountingIntegrityErrorCodes.JournalPostingConflict,
                        "Dokumen sumber sudah mempunyai jurnal dengan isi berbeda.");
                if (ownedTransaction is not null) await ownedTransaction.CommitAsync(ct);
                _logger?.LogInformation(
                    "JournalReplay BusinessId={BusinessId} OutletId={OutletId} JournalId={JournalId} ReferenceType={ReferenceType} ReferenceId={ReferenceId} PostingHash={PostingHash}",
                    draft.BusinessId, draft.OutletId, existing.Id, draft.ReferenceType, draft.ReferenceId, postingHash);
                return new JournalPostingResult(existing.Id, false);
            }

            var accountIds = draft.Lines.Select(x => x.ChartOfAccountId).Distinct().ToList();
            var accounts = await _dbContext.ChartOfAccounts.AsNoTracking()
                .Where(x => accountIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
            if (accounts.Count != accountIds.Count || accounts.Values.Any(x =>
                    !x.IsActive || x.BusinessId != draft.BusinessId ||
                    (x.OutletId.HasValue && x.OutletId != draft.OutletId)))
                throw new AccountingIntegrityException(AccountingIntegrityErrorCodes.AccountMappingInvalid,
                    "Satu atau lebih akun jurnal tidak aktif atau tidak sesuai business/outlet.");

            var totalDebit = draft.Lines.Sum(x => x.DebitAmount);
            var totalCredit = draft.Lines.Sum(x => x.CreditAmount);
            var now = DateTime.UtcNow;
            var journal = new JournalEntry
            {
                Id = Guid.NewGuid(), BusinessId = draft.BusinessId, OutletId = draft.OutletId,
                JournalNumber = draft.JournalNumber,
                JournalDate = draft.JournalDate,
                ReferenceType = draft.ReferenceType, ReferenceId = draft.ReferenceId,
                Description = draft.Description, Status = JournalEntryStatus.Posted,
                TotalDebit = totalDebit, TotalCredit = totalCredit, PostingHash = postingHash,
                ReversalOfJournalId = draft.ReversalOfJournalId, CreatedBy = draft.CreatedBy, CreatedAt = now
            };
            var lineNumber = 1;
            foreach (var line in draft.Lines)
            {
                journal.Lines.Add(new AccountTransaction
                {
                    Id = Guid.NewGuid(), BusinessId = draft.BusinessId, OutletId = draft.OutletId,
                    JournalEntryId = journal.Id, LineNumber = lineNumber++,
                    TrxDate = journal.JournalDate, TrxNumber = journal.JournalNumber,
                    ReferenceType = journal.ReferenceType, ReferenceId = journal.ReferenceId,
                    TrxEntity = AccountingTransactionEntity.Business, ChartOfAccountId = line.ChartOfAccountId,
                    DebitAmount = line.DebitAmount, CreditAmount = line.CreditAmount,
                    Note = line.Description ?? journal.Description, CreatedAt = now, UpdatedAt = now
                });
            }
            _dbContext.JournalEntries.Add(journal);
            await _dbContext.SaveChangesAsync(ct);
            if (ownedTransaction is not null) await ownedTransaction.CommitAsync(ct);

            _logger?.LogInformation(
                "JournalPosted BusinessId={BusinessId} OutletId={OutletId} JournalId={JournalId} ReferenceType={ReferenceType} ReferenceId={ReferenceId} TotalDebit={TotalDebit} TotalCredit={TotalCredit} PostingHash={PostingHash}",
                draft.BusinessId, draft.OutletId, journal.Id, draft.ReferenceType, draft.ReferenceId,
                totalDebit, totalCredit, postingHash);
            return new JournalPostingResult(journal.Id, true);
        }
        catch
        {
            if (ownedTransaction is not null) await ownedTransaction.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (ownedTransaction is not null) await ownedTransaction.DisposeAsync();
        }
    }

    public async Task<JournalPostingResult> ReverseAsync(
        Guid originalJournalId, string referenceType, Guid referenceId, string journalNumber,
        string description, Guid? createdBy, CancellationToken ct = default)
    {
        var original = await _dbContext.JournalEntries.AsNoTracking()
            .Include(x => x.Lines).SingleOrDefaultAsync(x => x.Id == originalJournalId, ct)
            ?? throw new AccountingIntegrityException(AccountingIntegrityErrorCodes.JournalReversalInvalid,
                "Jurnal asli tidak ditemukan.");
        if (original.Status == JournalEntryStatus.Reversed)
            throw new AccountingIntegrityException(AccountingIntegrityErrorCodes.JournalReversalInvalid,
                "Jurnal asli sudah pernah dibalik.");

        IDbContextTransaction? ownedTransaction = null;
        if (_dbContext.Database.IsRelational() && _dbContext.Database.CurrentTransaction is null)
            ownedTransaction = await _dbContext.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await PostAsync(new JournalDraft(
                original.BusinessId, original.OutletId, DateTime.UtcNow, journalNumber,
                referenceType, referenceId, description,
                original.Lines.OrderBy(x => x.LineNumber)
                    .Select(x => new JournalDraftLine(x.ChartOfAccountId, x.CreditAmount, x.DebitAmount, description)).ToList(),
                original.Id, createdBy), ct);

            if (result.Created)
            {
                if (_dbContext.Database.IsRelational())
                {
                    await _dbContext.Database.SqlQuery<Guid>(
                            $"SELECT fn_mark_journal_reversed({originalJournalId}) AS \"Value\"")
                        .SingleAsync(ct);
                }
                else
                {
                    var tracked = await _dbContext.JournalEntries.SingleAsync(x => x.Id == originalJournalId, ct);
                    tracked.Status = JournalEntryStatus.Reversed;
                    await _dbContext.SaveChangesAsync(ct);
                }
            }

            if (ownedTransaction is not null) await ownedTransaction.CommitAsync(ct);
            _logger?.LogInformation(
                "JournalReversed BusinessId={BusinessId} OutletId={OutletId} OriginalJournalId={OriginalJournalId} ReversalJournalId={ReversalJournalId} ReferenceType={ReferenceType} ReferenceId={ReferenceId}",
                original.BusinessId, original.OutletId, original.Id, result.JournalId, referenceType, referenceId);
            return result;
        }
        catch
        {
            if (ownedTransaction is not null) await ownedTransaction.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (ownedTransaction is not null) await ownedTransaction.DisposeAsync();
        }
    }

    public async Task<JournalEntryDto> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var journal = await Query().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("Jurnal tidak ditemukan.");
        return Map(journal);
    }

    public async Task<JournalEntryDto?> GetByReferenceAsync(
        string referenceType, Guid referenceId, CancellationToken ct = default)
    {
        var normalized = referenceType.Trim().ToLowerInvariant();
        var journal = await Query().SingleOrDefaultAsync(x =>
            x.ReferenceType == normalized && x.ReferenceId == referenceId, ct);
        return journal is null ? null : Map(journal);
    }

    public static string ComputeHash(JournalDraft draft)
    {
        var canonical = new StringBuilder()
            .Append(draft.BusinessId.ToString("N")).Append('|')
            .Append(draft.OutletId?.ToString("N") ?? "-").Append('|')
            .Append(draft.JournalDate.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)).Append('|')
            .Append(draft.ReferenceType.Trim().ToLowerInvariant()).Append('|')
            .Append(draft.ReferenceId.ToString("N")).Append('|')
            .Append(draft.ReversalOfJournalId?.ToString("N") ?? "-");
        var lineNumber = 1;
        foreach (var line in draft.Lines)
        {
            canonical.Append('|').Append(lineNumber++)
                .Append(':').Append(line.ChartOfAccountId.ToString("N"))
                .Append(':').Append(line.DebitAmount.ToString("0.00", CultureInfo.InvariantCulture))
                .Append(':').Append(line.CreditAmount.ToString("0.00", CultureInfo.InvariantCulture));
        }
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    private IQueryable<JournalEntry> Query()
        => _dbContext.JournalEntries.AsNoTracking()
            .Include(x => x.Lines).ThenInclude(x => x.ChartOfAccount);

    private static JournalEntryDto Map(JournalEntry journal)
        => new(journal.Id, journal.JournalNumber, journal.JournalDate,
            journal.ReferenceType, journal.ReferenceId, journal.Description, journal.Status,
            journal.TotalDebit, journal.TotalCredit, journal.TotalDebit == journal.TotalCredit,
            journal.PostingHash, journal.ReversalOfJournalId,
            journal.Lines.OrderBy(x => x.LineNumber).Select(x => new JournalLineDto(
                x.Id, x.LineNumber, x.ChartOfAccountId, x.ChartOfAccount.AccountCode,
                x.ChartOfAccount.AccountName, x.DebitAmount, x.CreditAmount, x.Note)).ToList());

    private static JournalDraft Normalize(JournalDraft draft)
    {
        if (string.IsNullOrWhiteSpace(draft.JournalNumber))
            throw new AccountingIntegrityException(AccountingIntegrityErrorCodes.JournalLineInvalid,
                "Nomor jurnal wajib diisi.");
        if (string.IsNullOrWhiteSpace(draft.ReferenceType))
            throw new AccountingIntegrityException(AccountingIntegrityErrorCodes.JournalLineInvalid,
                "Reference type jurnal wajib diisi.");

        return draft with
        {
            JournalNumber = draft.JournalNumber.Trim(),
            ReferenceType = draft.ReferenceType.Trim().ToLowerInvariant(),
            Description = string.IsNullOrWhiteSpace(draft.Description) ? "Jurnal otomatis" : draft.Description.Trim(),
            JournalDate = DateTime.SpecifyKind(draft.JournalDate, DateTimeKind.Utc),
            Lines = draft.Lines
                .Select(x => x with
                {
                    DebitAmount = decimal.Round(x.DebitAmount, 2),
                    CreditAmount = decimal.Round(x.CreditAmount, 2),
                    Description = string.IsNullOrWhiteSpace(x.Description) ? null : x.Description.Trim(),
                })
                .ToList(),
        };
    }

    private static void ValidateDraft(JournalDraft draft)
    {
        if (draft.Lines.Count < 2)
            throw new AccountingIntegrityException(AccountingIntegrityErrorCodes.JournalTooFewLines,
                "Jurnal minimal mempunyai dua baris.");
        if (draft.Lines.Any(x => x.ChartOfAccountId == Guid.Empty || x.DebitAmount < 0 || x.CreditAmount < 0 ||
                (x.DebitAmount > 0) == (x.CreditAmount > 0)))
            throw new AccountingIntegrityException(AccountingIntegrityErrorCodes.JournalLineInvalid,
                "Setiap baris jurnal harus mempunyai tepat satu nilai debit atau kredit positif.");
        var debit = draft.Lines.Sum(x => x.DebitAmount);
        var credit = draft.Lines.Sum(x => x.CreditAmount);
        if (debit <= 0 || credit <= 0 || debit != credit)
            throw new AccountingIntegrityException(AccountingIntegrityErrorCodes.JournalUnbalanced,
                $"Jurnal tidak seimbang. Debit {debit}, kredit {credit}.");
    }
}
