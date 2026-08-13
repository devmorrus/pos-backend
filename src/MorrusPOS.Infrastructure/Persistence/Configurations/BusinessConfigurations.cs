using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Infrastructure.Persistence.Configurations;

public class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
    public static readonly Guid SeedBusinessId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    public static readonly Guid SeedOwnerUserId = Guid.Parse("a4f78de1-8a9d-4e96-857e-399fa5b5f25a");

    public void Configure(EntityTypeBuilder<Business> builder)
    {
        builder.ToTable("businesses");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(20);
        builder.Property(x => x.SubscriptionStatus).HasMaxLength(20).IsRequired();
        builder.Property(x => x.SelectedPackage).HasMaxLength(50);

        // centralizing multitenancy configurations
        builder.HasMany(b => b.Users)
            .WithOne(u => u.Business)
            .HasForeignKey(u => u.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(b => b.Outlets)
            .WithOne(o => o.Business)
            .HasForeignKey(o => o.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany<Product>()
            .WithOne(p => p.Business)
            .HasForeignKey(p => p.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany<Category>()
            .WithOne(c => c.Business)
            .HasForeignKey(c => c.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany<Supplier>()
            .WithOne(s => s.Business)
            .HasForeignKey(s => s.BusinessId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(new Business
        {
            Id = SeedBusinessId,
            Name = "Morrus Demo Business",
            Category = "Retail",
            SubscriptionStatus = "Active",
            TrialStartDate = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            TrialEndDate = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
            SelectedPackage = "Development",
            OwnerId = SeedOwnerUserId,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}
