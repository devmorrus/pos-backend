using Microsoft.EntityFrameworkCore;
using MorrusPOS.Domain.Entities;

namespace MorrusPOS.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    // Fase 0 — Fondasi
    public DbSet<Outlet> Outlets => Set<Outlet>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // Fase 1 — Core POS
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionItem> TransactionItems => Set<TransactionItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Return> Returns => Set<Return>();

    // Fase 2 — Stok
    public DbSet<InventoryStock> InventoryStocks => Set<InventoryStock>();
    public DbSet<StockLedger> StockLedgers => Set<StockLedger>();
    public DbSet<StockOpname> StockOpnames => Set<StockOpname>();
    public DbSet<StockOpnameItem> StockOpnameItems => Set<StockOpnameItem>();

    // TODO Fase 3-7: Supplier, PurchaseOrder, Consignment, ChannelAccount,
    // StockTransfer, dst. Tambahkan entity class di Domain/Entities, lalu
    // daftarkan DbSet + konfigurasinya di sini mengikuti pola yang sama.

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
