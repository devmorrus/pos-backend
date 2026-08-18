using Microsoft.EntityFrameworkCore;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Application.Common.Interfaces;

namespace MorrusPOS.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    private readonly ICurrentUserService? _currentUserService;

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService currentUserService) : base(options)
    {
        _currentUserService = currentUserService;
    }

    public Guid? CurrentBusinessId => _currentUserService?.BusinessId;

    // SaaS Entities
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<ChartOfAccount> ChartOfAccounts => Set<ChartOfAccount>();
    public DbSet<CashFlow> CashFlows => Set<CashFlow>();
    public DbSet<AccountTransaction> AccountTransactions => Set<AccountTransaction>();

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
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<TransactionItem> TransactionItems => Set<TransactionItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Return> Returns => Set<Return>();
    public DbSet<CashierSession> CashierSessions => Set<CashierSession>();
    public DbSet<PettyCashExpense> PettyCashExpenses => Set<PettyCashExpense>();

    // Fase 2 — Stok
    public DbSet<InventoryStock> InventoryStocks => Set<InventoryStock>();
    public DbSet<StockLedger> StockLedgers => Set<StockLedger>();
    public DbSet<StockOpname> StockOpnames => Set<StockOpname>();
    public DbSet<StockOpnameItem> StockOpnameItems => Set<StockOpnameItem>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<StockTransferItem> StockTransferItems => Set<StockTransferItem>();

    // Fase 3-4 — Supplier & Pembelian
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<SupplierDebt> SupplierDebts => Set<SupplierDebt>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<SupplierReturn> SupplierReturns => Set<SupplierReturn>();
    public DbSet<SupplierReturnItem> SupplierReturnItems => Set<SupplierReturnItem>();

    // Fase 5 — Konsinyasi
    public DbSet<Consignment> Consignments => Set<Consignment>();
    public DbSet<ConsignmentItem> ConsignmentItems => Set<ConsignmentItem>();
    public DbSet<ConsignmentSale> ConsignmentSales => Set<ConsignmentSale>();
    public DbSet<ConsignmentSettlement> ConsignmentSettlements => Set<ConsignmentSettlement>();
    public DbSet<ConsignmentReturn> ConsignmentReturns => Set<ConsignmentReturn>();
    public DbSet<ConsignmentReturnItem> ConsignmentReturnItems => Set<ConsignmentReturnItem>();

    // Fase 6 — Integrasi Channel Online
    public DbSet<ChannelAccount> ChannelAccounts => Set<ChannelAccount>();
    public DbSet<ChannelSettlement> ChannelSettlements => Set<ChannelSettlement>();
    public DbSet<ChannelSettlementItem> ChannelSettlementItems => Set<ChannelSettlementItem>();
    public DbSet<IntegrationLog> IntegrationLogs => Set<IntegrationLog>();

    // Fase 7 — Pricing Engine
    public DbSet<TaxRule> TaxRules => Set<TaxRule>();
    public DbSet<ServiceChargeRule> ServiceChargeRules => Set<ServiceChargeRule>();
    public DbSet<PromoCampaign> PromoCampaigns => Set<PromoCampaign>();
    public DbSet<PromoCampaignTarget> PromoCampaignTargets => Set<PromoCampaignTarget>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<VoucherRedemption> VoucherRedemptions => Set<VoucherRedemption>();

    // Varian, Modifier, Resep & Batch UMKM
    public DbSet<ProductAttribute> ProductAttributes => Set<ProductAttribute>();
    public DbSet<ProductAttributeValue> ProductAttributeValues => Set<ProductAttributeValue>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<ModifierGroup> ModifierGroups => Set<ModifierGroup>();
    public DbSet<ModifierOption> ModifierOptions => Set<ModifierOption>();
    public DbSet<ProductRecipe> ProductRecipes => Set<ProductRecipe>();
    public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
    public DbSet<ReceivingNote> ReceivingNotes => Set<ReceivingNote>();
    public DbSet<ReceivingNoteItem> ReceivingNoteItems => Set<ReceivingNoteItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // SaaS Multitenancy Query Filters for Core Entities
        modelBuilder.Entity<Outlet>().HasQueryFilter(o => CurrentBusinessId == null || o.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<User>().HasQueryFilter(u => CurrentBusinessId == null || u.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<Product>().HasQueryFilter(p => CurrentBusinessId == null || p.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<Customer>().HasQueryFilter(c => CurrentBusinessId == null || c.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<Category>().HasQueryFilter(c => CurrentBusinessId == null || c.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<Supplier>().HasQueryFilter(s => CurrentBusinessId == null || s.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<ChartOfAccount>().HasQueryFilter(a => CurrentBusinessId == null || a.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<CashFlow>().HasQueryFilter(cf => CurrentBusinessId == null || cf.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<AccountTransaction>().HasQueryFilter(at => CurrentBusinessId == null || at.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<ProductAttribute>().HasQueryFilter(pa => CurrentBusinessId == null || pa.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<ModifierGroup>().HasQueryFilter(mg => CurrentBusinessId == null || mg.BusinessId == CurrentBusinessId);

        // SaaS Scoping for Operational Transactions & POS Sessions
        modelBuilder.Entity<Transaction>().HasQueryFilter(t => CurrentBusinessId == null || t.Outlet.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<CashierSession>().HasQueryFilter(cs => CurrentBusinessId == null || cs.Outlet.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<Payment>().HasQueryFilter(p => CurrentBusinessId == null || p.Transaction.Outlet.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<Return>().HasQueryFilter(r => CurrentBusinessId == null || r.Transaction.Outlet.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<PettyCashExpense>().HasQueryFilter(pe => CurrentBusinessId == null || pe.Outlet.BusinessId == CurrentBusinessId);

        // SaaS Scoping for Stocks & Inventory
        modelBuilder.Entity<InventoryStock>().HasQueryFilter(stock => CurrentBusinessId == null || stock.Outlet.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<StockLedger>().HasQueryFilter(sl => CurrentBusinessId == null || sl.Outlet.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<StockOpname>().HasQueryFilter(so => CurrentBusinessId == null || so.Outlet.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<StockTransfer>().HasQueryFilter(st => CurrentBusinessId == null || st.FromOutlet.BusinessId == CurrentBusinessId || st.ToOutlet.BusinessId == CurrentBusinessId);

        // SaaS Scoping for Suppliers, Procurement, and Purchase Orders
        modelBuilder.Entity<PurchaseOrder>().HasQueryFilter(po => CurrentBusinessId == null || po.Outlet.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<ReceivingNote>().HasQueryFilter(rn => CurrentBusinessId == null || rn.PurchaseOrder.Outlet.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<SupplierDebt>().HasQueryFilter(sd => CurrentBusinessId == null || sd.Supplier.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<SupplierPayment>().HasQueryFilter(sp => CurrentBusinessId == null || sp.Supplier.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<SupplierReturn>().HasQueryFilter(sr => CurrentBusinessId == null || sr.PurchaseOrder.Outlet.BusinessId == CurrentBusinessId);

        // SaaS Scoping for Consignments
        modelBuilder.Entity<Consignment>().HasQueryFilter(c => CurrentBusinessId == null || c.Outlet.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<ConsignmentSale>().HasQueryFilter(cs => CurrentBusinessId == null || cs.Supplier.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<ConsignmentSettlement>().HasQueryFilter(cset => CurrentBusinessId == null || cset.Outlet.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<ConsignmentReturn>().HasQueryFilter(cr => CurrentBusinessId == null || cr.Outlet.BusinessId == CurrentBusinessId);

        // SaaS Scoping for Online Channels Integration
        modelBuilder.Entity<ChannelAccount>().HasQueryFilter(ca => CurrentBusinessId == null || ca.Outlet.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<ChannelSettlement>().HasQueryFilter(cset => CurrentBusinessId == null || cset.ChannelAccount.Outlet.BusinessId == CurrentBusinessId);

        // SaaS Scoping for Pricing
        modelBuilder.Entity<TaxRule>().HasQueryFilter(tr => CurrentBusinessId == null || tr.Outlet.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<ServiceChargeRule>().HasQueryFilter(sr => CurrentBusinessId == null || sr.Outlet.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<PromoCampaign>().HasQueryFilter(pc => CurrentBusinessId == null || pc.Outlet.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<Voucher>().HasQueryFilter(v => CurrentBusinessId == null || v.Outlet.BusinessId == CurrentBusinessId);
        modelBuilder.Entity<VoucherRedemption>().HasQueryFilter(vr => CurrentBusinessId == null || vr.Transaction.Outlet.BusinessId == CurrentBusinessId);

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AutoAssignBusinessId();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        AutoAssignBusinessId();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AutoAssignBusinessId();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override int SaveChanges()
    {
        AutoAssignBusinessId();
        return base.SaveChanges();
    }

    private void AutoAssignBusinessId()
    {
        var currentBusinessId = CurrentBusinessId;
        if (currentBusinessId.HasValue)
        {
            var addedEntries = ChangeTracker.Entries()
                .Where(e => e.State == EntityState.Added);

            foreach (var entry in addedEntries)
            {
                var businessIdProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "BusinessId");
                if (businessIdProperty != null)
                {
                    var currentValue = businessIdProperty.CurrentValue;
                    if (currentValue == null || (currentValue is Guid guidVal && guidVal == Guid.Empty))
                    {
                        businessIdProperty.CurrentValue = currentBusinessId.Value;
                    }
                }
            }
        }
    }
}
