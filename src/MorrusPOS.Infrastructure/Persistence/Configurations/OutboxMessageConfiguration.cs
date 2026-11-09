using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("outbox_messages", table =>
        {
            table.HasCheckConstraint("CK_outbox_messages_Status",
                "\"Status\" IN ('pending','processing','processed','failed','dead_letter')");
            table.HasCheckConstraint("CK_outbox_messages_Attempts",
                "\"AttemptCount\" BETWEEN 0 AND \"MaxAttempts\" AND \"MaxAttempts\" BETWEEN 1 AND 20 AND \"ManualReplayCount\" >= 0");
            table.HasCheckConstraint("CK_outbox_messages_PayloadSize",
                "octet_length(\"PayloadJson\"::text) <= 262144");
            table.HasCheckConstraint("CK_outbox_messages_LockState",
                "(\"Status\" = 'processing' AND \"LockId\" IS NOT NULL AND \"LockedAtUtc\" IS NOT NULL AND \"LockedBy\" IS NOT NULL) OR " +
                "(\"Status\" <> 'processing' AND \"LockId\" IS NULL AND \"LockedAtUtc\" IS NULL AND \"LockedBy\" IS NULL)");
            table.HasCheckConstraint("CK_outbox_messages_CompletedState",
                "(\"Status\" <> 'processed' OR \"ProcessedAtUtc\" IS NOT NULL) AND " +
                "(\"Status\" <> 'dead_letter' OR \"DeadLetteredAtUtc\" IS NOT NULL)");
        });

        builder.HasKey(x => x.Id);
        builder.Property(x => x.SequenceNo).UseIdentityAlwaysColumn();
        builder.HasIndex(x => x.SequenceNo).IsUnique();
        builder.Property(x => x.EventType).HasMaxLength(150).IsRequired();
        builder.Property(x => x.SchemaVersion).IsRequired();
        builder.Property(x => x.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.DeduplicationKey).HasMaxLength(250);
        builder.Property(x => x.CorrelationId).HasMaxLength(64);
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.Property(x => x.LockedBy).HasMaxLength(150);
        builder.Property(x => x.LastErrorCode).HasMaxLength(100);
        builder.Property(x => x.LastErrorMessage).HasMaxLength(2000);
        builder.Property(x => x.LastReplayedBy).HasMaxLength(150);
        builder.Property(x => x.LastReplayReason).HasMaxLength(500);

        builder.HasIndex(x => new { x.Status, x.AvailableAtUtc, x.SequenceNo })
            .HasFilter("\"Status\" IN ('pending','failed')");
        builder.HasIndex(x => new { x.Status, x.LockedAtUtc })
            .HasFilter("\"Status\" = 'processing'");
        builder.HasIndex(x => new { x.Status, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.BusinessId, x.OutletId, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.BusinessId, x.EventType, x.DeduplicationKey })
            .IsUnique()
            .HasFilter("\"DeduplicationKey\" IS NOT NULL");

        builder.HasOne(x => x.Business)
            .WithMany()
            .HasForeignKey(x => x.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Outlet)
            .WithMany()
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
