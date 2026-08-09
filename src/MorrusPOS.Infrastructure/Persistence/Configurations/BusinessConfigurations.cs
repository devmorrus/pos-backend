using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Infrastructure.Persistence.Configurations;

public class BusinessConfiguration : IEntityTypeConfiguration<Business>
{
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
    }
}
