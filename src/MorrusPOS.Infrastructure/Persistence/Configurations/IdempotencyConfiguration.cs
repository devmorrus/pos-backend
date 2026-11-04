using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Infrastructure.Persistence.Configurations;

public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("idempotency_records", t =>
        {
            t.HasCheckConstraint("CK_idempotency_records_Operation",
                "\"Operation\" IN ('checkout','refund','supplier_payment')");
            t.HasCheckConstraint("CK_idempotency_records_RequestHash",
                "length(\"RequestHash\") = 64");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Operation).HasMaxLength(40).IsRequired();
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsFixedLength().IsRequired();
        builder.Property(x => x.ResourceType).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => new { x.BusinessId, x.Operation, x.IdempotencyKey }).IsUnique();
        builder.HasIndex(x => new { x.BusinessId, x.ResourceType, x.ResourceId });
        builder.HasIndex(x => new { x.BusinessId, x.OutletId, x.CreatedAt });
        builder.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Outlet).WithMany().HasForeignKey(x => x.OutletId).OnDelete(DeleteBehavior.Restrict);
    }
}
