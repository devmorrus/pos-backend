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
        builder.ToTable("account_transactions");
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
    }
}
