using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Infrastructure.Persistence.Configurations;

public class StockLocationConfiguration : IEntityTypeConfiguration<StockLocation>
{
    public void Configure(EntityTypeBuilder<StockLocation> builder)
    {
        builder.ToTable("stock_locations", t =>
        {
            t.HasCheckConstraint("CK_stock_locations_Type", "\"Type\" IN ('outlet','warehouse')");
            t.HasCheckConstraint("CK_stock_locations_OutletLink",
                "(\"Type\"='outlet' AND \"OutletId\" IS NOT NULL) OR (\"Type\"='warehouse' AND \"OutletId\" IS NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Type).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.Phone).HasMaxLength(30);

        builder.HasIndex(x => new { x.BusinessId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.BusinessId, x.OutletId })
            .IsUnique()
            .HasFilter("\"OutletId\" IS NOT NULL");
        builder.HasIndex(x => new { x.BusinessId, x.Type, x.IsActive, x.Name });

        builder.HasOne(x => x.Outlet)
            .WithMany()
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class UserStockLocationAccessConfiguration : IEntityTypeConfiguration<UserStockLocationAccess>
{
    public void Configure(EntityTypeBuilder<UserStockLocationAccess> builder)
    {
        builder.ToTable("user_stock_location_access");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.BusinessId, x.UserId, x.StockLocationId }).IsUnique();
        builder.HasIndex(x => new { x.BusinessId, x.UserId })
            .IsUnique()
            .HasFilter("\"IsDefault\" = TRUE");

        builder.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.StockLocation)
            .WithMany()
            .HasForeignKey(x => x.StockLocationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PosRegisterConfiguration : IEntityTypeConfiguration<PosRegister>
{
    public void Configure(EntityTypeBuilder<PosRegister> builder)
    {
        builder.ToTable("pos_registers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(x => new { x.BusinessId, x.OutletId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.BusinessId, x.StockLocationId });

        builder.HasOne(x => x.Outlet)
            .WithMany()
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.StockLocation)
            .WithMany()
            .HasForeignKey(x => x.StockLocationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PosDeviceConfiguration : IEntityTypeConfiguration<PosDevice>
{
    public void Configure(EntityTypeBuilder<PosDevice> builder)
    {
        builder.ToTable("pos_devices", t =>
        {
            t.HasCheckConstraint("CK_pos_devices_Status", "\"Status\" IN ('active','revoked')");
            t.HasCheckConstraint("CK_pos_devices_Platform",
                "\"Platform\" IN ('web','android','ios','windows','macos','linux')");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Platform).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(20).IsRequired();
        builder.Property(x => x.AppVersion).HasMaxLength(50);
        builder.Property(x => x.CredentialHash).IsRequired();

        builder.HasIndex(x => new { x.BusinessId, x.InstallationId }).IsUnique();
        builder.HasIndex(x => x.RegisterId)
            .IsUnique()
            .HasFilter("\"Status\" = 'active'");

        builder.HasOne(x => x.Register)
            .WithMany()
            .HasForeignKey(x => x.RegisterId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class PosDevicePairingCodeConfiguration : IEntityTypeConfiguration<PosDevicePairingCode>
{
    public void Configure(EntityTypeBuilder<PosDevicePairingCode> builder)
    {
        builder.ToTable("pos_device_pairing_codes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CodeHash).IsRequired();
        builder.HasIndex(x => new { x.BusinessId, x.CodeHash });
        builder.HasIndex(x => new { x.BusinessId, x.RegisterId, x.ExpiresAtUtc });
        builder.HasIndex(x => x.ExpiresAtUtc);

        builder.HasOne(x => x.Register)
            .WithMany()
            .HasForeignKey(x => x.RegisterId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
