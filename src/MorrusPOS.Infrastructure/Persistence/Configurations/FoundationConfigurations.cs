using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Infrastructure.Persistence.Configurations;

public class OutletConfiguration : IEntityTypeConfiguration<Outlet>
{
    public void Configure(EntityTypeBuilder<Outlet> builder)
    {
        builder.ToTable("outlets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Phone).HasMaxLength(20);

        builder.HasData(new Outlet
        {
            Id = Guid.Parse("8bba5427-017e-40fb-886f-5e4c6c9a3809"),
            BusinessId = BusinessConfiguration.SeedBusinessId,
            Name = "Outlet Utama",
            Code = "OUT001",
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(50).IsRequired();

        builder.HasData(
            new Role { Id = Guid.Parse("e1a7b077-44a3-4b63-95e0-59a8501170ea"), Name = "Owner", Description = "Pemilik Bisnis (Akses penuh semua outlet)" },
            new Role { Id = Guid.Parse("d54f590a-6e54-4f05-8461-8ff62dfd8d4c"), Name = "Admin", Description = "Administrator Sistem" },
            new Role { Id = Guid.Parse("a3d75ea9-ec36-4e56-9a2c-d95ea7b2671f"), Name = "KepalaCabang", Description = "Pengelola Toko/Cabang" },
            new Role { Id = Guid.Parse("b5e7d5a9-4674-4b5b-a81d-e59fa24285b0"), Name = "Kasir", Description = "Operator Kasir Penjualan" },
            new Role { Id = Guid.Parse("c457d5a9-5e74-4e2b-a71d-e59fa24285c1"), Name = "Gudang", Description = "Staf Logistik dan Persediaan" },
            new Role { Id = Guid.Parse("d667d5a9-6e74-4e2b-b81d-e59fa24285d2"), Name = "Keuangan", Description = "Staf Akuntansi dan Arus Kas" }
        );
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();

        builder.HasData(
            new Permission { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Code = "transaction.create", Description = "Membuat Transaksi Penjualan" },
            new Permission { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Code = "transaction.void", Description = "Membatalkan Transaksi" },
            new Permission { Id = Guid.Parse("33333333-3333-3333-3333-333333333333"), Code = "product.manage", Description = "Mengelola Produk dan Kategori" },
            new Permission { Id = Guid.Parse("44444444-4444-4444-4444-444444444444"), Code = "stock.manage", Description = "Mengelola Stok (Opname & Transfer)" },
            new Permission { Id = Guid.Parse("55555555-5555-5555-5555-555555555555"), Code = "supplier.manage", Description = "Mengelola Supplier dan PO" },
            new Permission { Id = Guid.Parse("66666666-6666-6666-6666-666666666666"), Code = "consignment.manage", Description = "Mengelola Barang Titipan/Konsinyasi" },
            new Permission { Id = Guid.Parse("77777777-7777-7777-7777-777777777777"), Code = "report.view", Description = "Melihat Laporan Laba Rugi & Dashboard" },
            new Permission { Id = Guid.Parse("88888888-8888-8888-8888-888888888888"), Code = "supplier_return.manage", Description = "Mengelola retur supplier" },
            new Permission { Id = Guid.Parse("99999999-9999-9999-9999-999999999999"), Code = "channel_settlement.manage", Description = "Mengelola channel account dan settlement channel" },
            new Permission { Id = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), Code = "pricing.manage", Description = "Mengelola pricing rule, promo, voucher, pajak, dan service charge" },
            new Permission { Id = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"), Code = "customer.manage", Description = "Mengelola master customer dan member dasar" },
            new Permission { Id = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc"), Code = "customer.view", Description = "Melihat dan lookup customer untuk transaksi kasir" },
            new Permission { Id = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd"), Code = "account.manage", Description = "Mengelola master chart of accounts" },
            new Permission { Id = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"), Code = "cashflow.create", Description = "Membuat transaksi pemasukan dan pengeluaran toko" },
            new Permission { Id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"), Code = "cashflow.view", Description = "Melihat histori transaksi cash flow manual" },
            new Permission { Id = Guid.Parse("12121212-1212-1212-1212-121212121212"), Code = "report.cashflow.view", Description = "Melihat laporan arus kas" },
            new Permission { Id = Guid.Parse("13131313-1313-1313-1313-131313131313"), Code = "report.profitloss_accounting.view", Description = "Melihat laporan laba rugi akuntansi" }
        );
    }
}

public class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        builder.ToTable("role_permissions");
        builder.HasKey(x => new { x.RoleId, x.PermissionId });

        builder.HasOne(x => x.Role)
            .WithMany(r => r.RolePermissions)
            .HasForeignKey(x => x.RoleId);

        builder.HasOne(x => x.Permission)
            .WithMany(p => p.RolePermissions)
            .HasForeignKey(x => x.PermissionId);

        var ownerId = Guid.Parse("e1a7b077-44a3-4b63-95e0-59a8501170ea");
        var adminId = Guid.Parse("d54f590a-6e54-4f05-8461-8ff62dfd8d4c");
        var kepalaCabangId = Guid.Parse("a3d75ea9-ec36-4e56-9a2c-d95ea7b2671f");
        var kasirId = Guid.Parse("b5e7d5a9-4674-4b5b-a81d-e59fa24285b0");
        var gudangId = Guid.Parse("c457d5a9-5e74-4e2b-a71d-e59fa24285c1");
        var keuanganId = Guid.Parse("d667d5a9-6e74-4e2b-b81d-e59fa24285d2");

        var pTxCreate = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var pTxVoid = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var pProdManage = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var pStockManage = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var pSupplierManage = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var pConsignmentManage = Guid.Parse("66666666-6666-6666-6666-666666666666");
        var pReportView = Guid.Parse("77777777-7777-7777-7777-777777777777");
        var pSupplierReturnManage = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var pChannelSettlementManage = Guid.Parse("99999999-9999-9999-9999-999999999999");
        var pPricingManage = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var pCustomerManage = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var pCustomerView = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var pAccountManage = Guid.Parse("dddddddd-dddd-dddd-dddd-dddddddddddd");
        var pCashflowCreate = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
        var pCashflowView = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        var pCashflowReport = Guid.Parse("12121212-1212-1212-1212-121212121212");
        var pProfitLossAccountingReport = Guid.Parse("13131313-1313-1313-1313-131313131313");

        builder.HasData(
            // Owner gets all
            new RolePermission { RoleId = ownerId, PermissionId = pTxCreate },
            new RolePermission { RoleId = ownerId, PermissionId = pTxVoid },
            new RolePermission { RoleId = ownerId, PermissionId = pProdManage },
            new RolePermission { RoleId = ownerId, PermissionId = pStockManage },
            new RolePermission { RoleId = ownerId, PermissionId = pSupplierManage },
            new RolePermission { RoleId = ownerId, PermissionId = pConsignmentManage },
            new RolePermission { RoleId = ownerId, PermissionId = pReportView },
            new RolePermission { RoleId = ownerId, PermissionId = pSupplierReturnManage },
            new RolePermission { RoleId = ownerId, PermissionId = pChannelSettlementManage },
            new RolePermission { RoleId = ownerId, PermissionId = pPricingManage },
            new RolePermission { RoleId = ownerId, PermissionId = pCustomerManage },
            new RolePermission { RoleId = ownerId, PermissionId = pCustomerView },
            new RolePermission { RoleId = ownerId, PermissionId = pAccountManage },
            new RolePermission { RoleId = ownerId, PermissionId = pCashflowCreate },
            new RolePermission { RoleId = ownerId, PermissionId = pCashflowView },
            new RolePermission { RoleId = ownerId, PermissionId = pCashflowReport },
            new RolePermission { RoleId = ownerId, PermissionId = pProfitLossAccountingReport },

            // Admin gets all
            new RolePermission { RoleId = adminId, PermissionId = pTxCreate },
            new RolePermission { RoleId = adminId, PermissionId = pTxVoid },
            new RolePermission { RoleId = adminId, PermissionId = pProdManage },
            new RolePermission { RoleId = adminId, PermissionId = pStockManage },
            new RolePermission { RoleId = adminId, PermissionId = pSupplierManage },
            new RolePermission { RoleId = adminId, PermissionId = pConsignmentManage },
            new RolePermission { RoleId = adminId, PermissionId = pReportView },
            new RolePermission { RoleId = adminId, PermissionId = pSupplierReturnManage },
            new RolePermission { RoleId = adminId, PermissionId = pChannelSettlementManage },
            new RolePermission { RoleId = adminId, PermissionId = pPricingManage },
            new RolePermission { RoleId = adminId, PermissionId = pCustomerManage },
            new RolePermission { RoleId = adminId, PermissionId = pCustomerView },
            new RolePermission { RoleId = adminId, PermissionId = pAccountManage },
            new RolePermission { RoleId = adminId, PermissionId = pCashflowCreate },
            new RolePermission { RoleId = adminId, PermissionId = pCashflowView },
            new RolePermission { RoleId = adminId, PermissionId = pCashflowReport },
            new RolePermission { RoleId = adminId, PermissionId = pProfitLossAccountingReport },

            // Kepala Cabang gets operational branch permissions
            new RolePermission { RoleId = kepalaCabangId, PermissionId = pTxCreate },
            new RolePermission { RoleId = kepalaCabangId, PermissionId = pProdManage },
            new RolePermission { RoleId = kepalaCabangId, PermissionId = pStockManage },
            new RolePermission { RoleId = kepalaCabangId, PermissionId = pCustomerManage },
            new RolePermission { RoleId = kepalaCabangId, PermissionId = pCustomerView },

            // Kasir gets transaction.create
            new RolePermission { RoleId = kasirId, PermissionId = pTxCreate },
            new RolePermission { RoleId = kasirId, PermissionId = pCustomerView },

            // Gudang gets product & stock
            new RolePermission { RoleId = gudangId, PermissionId = pProdManage },
            new RolePermission { RoleId = gudangId, PermissionId = pStockManage },

            // Keuangan gets supplier, consignment, report
            new RolePermission { RoleId = keuanganId, PermissionId = pSupplierManage },
            new RolePermission { RoleId = keuanganId, PermissionId = pConsignmentManage },
            new RolePermission { RoleId = keuanganId, PermissionId = pReportView },
            new RolePermission { RoleId = keuanganId, PermissionId = pSupplierReturnManage },
            new RolePermission { RoleId = keuanganId, PermissionId = pChannelSettlementManage },
            new RolePermission { RoleId = keuanganId, PermissionId = pPricingManage },
            new RolePermission { RoleId = keuanganId, PermissionId = pCustomerManage },
            new RolePermission { RoleId = keuanganId, PermissionId = pCustomerView },
            new RolePermission { RoleId = keuanganId, PermissionId = pAccountManage },
            new RolePermission { RoleId = keuanganId, PermissionId = pCashflowCreate },
            new RolePermission { RoleId = keuanganId, PermissionId = pCashflowView },
            new RolePermission { RoleId = keuanganId, PermissionId = pCashflowReport },
            new RolePermission { RoleId = keuanganId, PermissionId = pProfitLossAccountingReport }
        );
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(150).IsRequired();
        builder.Property(x => x.Email).HasMaxLength(150).IsRequired();
        builder.HasIndex(x => x.Email).IsUnique();
        builder.Property(x => x.PasswordHash).HasMaxLength(255).IsRequired();

        // OutletId nullable = akses semua outlet (Owner). Keputusan arsitektur:
        // tidak pakai tabel pivot user_outlets.
        builder.HasOne(x => x.Outlet)
            .WithMany(o => o.Users)
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(x => x.Role)
            .WithMany(r => r.Users)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(new User
        {
            Id = Guid.Parse("a4f78de1-8a9d-4e96-857e-399fa5b5f25a"),
            BusinessId = BusinessConfiguration.SeedBusinessId,
            OutletId = null, // Owner access to all outlets
            RoleId = Guid.Parse("e1a7b077-44a3-4b63-95e0-59a8501170ea"), // Owner Role
            Name = "Morrus Owner",
            Email = "owner@morruspos.com",
            PasswordHash = "$2a$11$6ujtfLVtlAZfBxEm.Zkvc.QvceWYFEa8tDpAhsaBWianak4Lb6QDS", // bcrypt for 'owner123'
            IsActive = true,
            CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        });
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.EntityType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(50).IsRequired();
        builder.Property(x => x.OldValueJson).HasColumnType("jsonb");
        builder.Property(x => x.NewValueJson).HasColumnType("jsonb");

        builder.HasOne(x => x.User)
            .WithMany(u => u.AuditLogs)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.Outlet)
            .WithMany()
            .HasForeignKey(x => x.OutletId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Token).HasMaxLength(255).IsRequired();
        builder.HasIndex(x => x.Token).IsUnique();

        builder.HasOne(x => x.User)
            .WithMany(u => u.RefreshTokens)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
