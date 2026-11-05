using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Infrastructure.Persistence.Configurations;

public class ChartOfAccountConfiguration : IEntityTypeConfiguration<ChartOfAccount>
{
    public void Configure(EntityTypeBuilder<ChartOfAccount> builder)
    {
        builder.ToTable("chart_of_accounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.AccountCode).HasMaxLength(30).IsRequired();
        builder.Property(x => x.AccountName).HasMaxLength(150).IsRequired();
        builder.Property(x => x.AccountType).HasMaxLength(20).IsRequired();
        builder.Property(x => x.IsCashBank).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();

        builder.HasIndex(x => new { x.BusinessId, x.AccountCode }).IsUnique();
        builder.HasIndex(x => new { x.BusinessId, x.AccountType });
        builder.HasIndex(x => new { x.BusinessId, x.IsCashBank });

        builder.HasOne(x => x.Business)
            .WithMany(b => b.ChartOfAccounts)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Outlet)
            .WithMany(o => o.ChartOfAccounts)
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ParentAccount)
            .WithMany(x => x.Children)
            .HasForeignKey(x => x.ParentAccountId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CashFlowConfiguration : IEntityTypeConfiguration<CashFlow>
{
    public void Configure(EntityTypeBuilder<CashFlow> builder)
    {
        builder.ToTable("cash_flows");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TrxNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.TrxType).HasMaxLength(10).IsRequired();
        builder.Property(x => x.TrxEntity).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Amount).HasColumnType("decimal(14,2)");
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.Property(x => x.AttachmentUrl).HasMaxLength(500);

        builder.HasIndex(x => new { x.BusinessId, x.TrxNumber }).IsUnique();
        builder.HasIndex(x => new { x.BusinessId, x.TrxDate });
        builder.HasIndex(x => new { x.BusinessId, x.TrxType });

        builder.HasOne(x => x.Business)
            .WithMany(b => b.CashFlows)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Outlet)
            .WithMany(o => o.CashFlows)
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.FromChartOfAccount)
            .WithMany(x => x.SourceCashFlows)
            .HasForeignKey(x => x.FromChartOfAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.ToChartOfAccount)
            .WithMany(x => x.DestinationCashFlows)
            .HasForeignKey(x => x.ToChartOfAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CreatedByUser)
            .WithMany(u => u.CashFlowsCreated)
            .HasForeignKey(x => x.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AccountTransactionConfiguration : IEntityTypeConfiguration<AccountTransaction>
{
    public void Configure(EntityTypeBuilder<AccountTransaction> builder)
    {
        builder.ToTable("account_transactions", t =>
        {
            t.HasCheckConstraint("CK_account_transactions_DebitCredit",
                "(\"DebitAmount\" > 0 AND \"CreditAmount\" = 0) OR (\"CreditAmount\" > 0 AND \"DebitAmount\" = 0)");
            t.HasCheckConstraint("CK_account_transactions_LineNumber", "\"LineNumber\" > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TrxNumber).HasMaxLength(40).IsRequired();
        builder.Property(x => x.ReferenceType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.TrxEntity).HasMaxLength(20).IsRequired();
        builder.Property(x => x.DebitAmount).HasColumnType("decimal(14,2)");
        builder.Property(x => x.CreditAmount).HasColumnType("decimal(14,2)");
        builder.Property(x => x.Note).HasMaxLength(500);

        builder.HasIndex(x => new { x.BusinessId, x.TrxDate });
        builder.HasIndex(x => new { x.BusinessId, x.TrxEntity });
        builder.HasIndex(x => new { x.BusinessId, x.ReferenceType, x.ReferenceId });
        builder.HasIndex(x => new { x.JournalEntryId, x.LineNumber }).IsUnique();
        builder.HasIndex(x => new { x.BusinessId, x.ChartOfAccountId, x.TrxDate });

        builder.HasOne(x => x.Business)
            .WithMany(b => b.AccountTransactions)
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Outlet)
            .WithMany(o => o.AccountTransactions)
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.ChartOfAccount)
            .WithMany(a => a.AccountTransactions)
            .HasForeignKey(x => x.ChartOfAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.JournalEntry)
            .WithMany(x => x.Lines)
            .HasForeignKey(x => x.JournalEntryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class AccountingAccountMappingConfiguration : IEntityTypeConfiguration<AccountingAccountMapping>
{
    public void Configure(EntityTypeBuilder<AccountingAccountMapping> builder)
    {
        builder.ToTable("accounting_account_mappings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Role).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => new { x.BusinessId, x.Role }).IsUnique()
            .HasFilter("\"OutletId\" IS NULL");
        builder.HasIndex(x => new { x.BusinessId, x.OutletId, x.Role }).IsUnique()
            .HasFilter("\"OutletId\" IS NOT NULL");
        builder.HasIndex(x => x.ChartOfAccountId);

        builder.HasOne(x => x.Business).WithMany()
            .HasForeignKey(x => x.BusinessId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Outlet).WithMany()
            .HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ChartOfAccount).WithMany()
            .HasForeignKey(x => x.ChartOfAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class JournalEntryConfiguration : IEntityTypeConfiguration<JournalEntry>
{
    public void Configure(EntityTypeBuilder<JournalEntry> builder)
    {
        builder.ToTable("journal_entries", t =>
        {
            t.HasCheckConstraint("CK_journal_entries_Status", "\"Status\" IN ('posted','reversed')");
            t.HasCheckConstraint("CK_journal_entries_Balanced",
                "\"TotalDebit\" > 0 AND \"TotalCredit\" > 0 AND \"TotalDebit\" = \"TotalCredit\"");
            t.HasCheckConstraint("CK_journal_entries_PostingHash", "length(\"PostingHash\") = 64");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.JournalNumber).HasMaxLength(60).IsRequired();
        builder.Property(x => x.ReferenceType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.PostingHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(x => x.TotalDebit).HasColumnType("decimal(14,2)");
        builder.Property(x => x.TotalCredit).HasColumnType("decimal(14,2)");
        builder.HasIndex(x => new { x.BusinessId, x.ReferenceType, x.ReferenceId }).IsUnique();
        builder.HasIndex(x => new { x.BusinessId, x.JournalDate });
        builder.HasIndex(x => new { x.BusinessId, x.OutletId, x.JournalDate });
        builder.HasOne(x => x.Outlet).WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReversalOfJournal).WithMany(x => x.Reversals)
            .HasForeignKey(x => x.ReversalOfJournalId).OnDelete(DeleteBehavior.Restrict);
    }
}
