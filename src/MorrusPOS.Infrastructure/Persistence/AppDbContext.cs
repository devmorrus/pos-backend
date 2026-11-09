using Microsoft.EntityFrameworkCore;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Application.Common.Interfaces;

namespace MorrusPOS.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    private readonly ICurrentUserService? _currentUserService;

    protected bool SystemContext { get; }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected AppDbContext(DbContextOptions options, bool systemContext) : base(options)
    {
        SystemContext = systemContext;
    }

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService currentUserService) : base(options)
    {
        _currentUserService = currentUserService;
    }

    public Guid? CurrentBusinessId => _currentUserService?.BusinessId;

    // SaaS Entities
    public DbSet<Business> Businesses => Set<Business>();
    public DbSet<Capability> Capabilities => Set<Capability>();
    public DbSet<IndustryProfile> IndustryProfiles => Set<IndustryProfile>();
    public DbSet<IndustryProfileCapability> IndustryProfileCapabilities => Set<IndustryProfileCapability>();
    public DbSet<BusinessCapabilityOverride> BusinessCapabilityOverrides => Set<BusinessCapabilityOverride>();
    public DbSet<OutletCapabilityOverride> OutletCapabilityOverrides => Set<OutletCapabilityOverride>();
    public DbSet<CapabilityChangeLog> CapabilityChangeLogs => Set<CapabilityChangeLog>();
    public DbSet<ChartOfAccount> ChartOfAccounts => Set<ChartOfAccount>();
    public DbSet<CashFlow> CashFlows => Set<CashFlow>();
    public DbSet<AccountTransaction> AccountTransactions => Set<AccountTransaction>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<AccountingAccountMapping> AccountingAccountMappings => Set<AccountingAccountMapping>();

    // Fase 0 — Fondasi
    public DbSet<Outlet> Outlets => Set<Outlet>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
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
    public DbSet<StockLocation> StockLocations => Set<StockLocation>();
    public DbSet<UserStockLocationAccess> UserStockLocationAccesses => Set<UserStockLocationAccess>();
    public DbSet<PosRegister> PosRegisters => Set<PosRegister>();
    public DbSet<PosDevice> PosDevices => Set<PosDevice>();
    public DbSet<PosDevicePairingCode> PosDevicePairingCodes => Set<PosDevicePairingCode>();
    public DbSet<InventoryStock> InventoryStocks => Set<InventoryStock>();
    public DbSet<StockLedger> StockLedgers => Set<StockLedger>();
    public DbSet<StockReconciliationRun> StockReconciliationRuns => Set<StockReconciliationRun>();
    public DbSet<StockReconciliationItem> StockReconciliationItems => Set<StockReconciliationItem>();
    public DbSet<StockOpname> StockOpnames => Set<StockOpname>();
    public DbSet<StockOpnameItem> StockOpnameItems => Set<StockOpnameItem>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<StockTransferItem> StockTransferItems => Set<StockTransferItem>();
    public DbSet<ChannelStockPolicy> ChannelStockPolicies => Set<ChannelStockPolicy>();

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
    public DbSet<GoBizIntegration> GoBizIntegrations => Set<GoBizIntegration>();
    public DbSet<GoBizOAuthState> GoBizOAuthStates => Set<GoBizOAuthState>();
    public DbSet<GoBizClientConfig> GoBizClientConfigs => Set<GoBizClientConfig>();
    public DbSet<GoBizDirectIntegration> GoBizDirectIntegrations => Set<GoBizDirectIntegration>();
    public DbSet<GoBizProductMapping> GoBizProductMappings => Set<GoBizProductMapping>();
    public DbSet<GoBizOrderInbox> GoBizOrderInboxes => Set<GoBizOrderInbox>();

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
    public DbSet<ModifierGroupProduct> ModifierGroupProducts => Set<ModifierGroupProduct>();
    public DbSet<ProductRecipe> ProductRecipes => Set<ProductRecipe>();
    public DbSet<ProductBatch> ProductBatches => Set<ProductBatch>();
    public DbSet<ProductSerial> ProductSerials => Set<ProductSerial>();
    public DbSet<ReceivingNote> ReceivingNotes => Set<ReceivingNote>();
    public DbSet<ReceivingNoteItem> ReceivingNoteItems => Set<ReceivingNoteItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        // Explicit join entity so the many-to-many bridge also carries tenant
        // identity and can be protected by RLS like every other tenant table.
        modelBuilder.Entity<Product>()
            .HasMany(p => p.ModifierGroups)
            .WithMany(m => m.Products)
            .UsingEntity<ModifierGroupProduct>(
                right => right.HasOne(j => j.ModifierGroup)
                    .WithMany()
                    .HasForeignKey(j => j.ModifierGroupsId),
                left => left.HasOne(j => j.Product)
                    .WithMany()
                    .HasForeignKey(j => j.ProductsId),
                join =>
                {
                    join.ToTable("ModifierGroupProduct");
                    join.HasKey(j => new { j.ModifierGroupsId, j.ProductsId });
                });

        // SaaS Multitenancy: all tenant-owned rows carry a direct BusinessId.
        // Fail-closed: tanpa business context, hasil query = kosong. Data lintas
        // business tidak pernah terlihat dari normal app context.

        modelBuilder.Entity<Business>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.Id == CurrentBusinessId));
        modelBuilder.Entity<BusinessCapabilityOverride>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<OutletCapabilityOverride>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<CapabilityChangeLog>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<Outlet>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<User>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<Product>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<Customer>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<Category>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<Supplier>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<ChartOfAccount>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<CashFlow>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<AccountTransaction>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<JournalEntry>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<AccountingAccountMapping>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<ProductAttribute>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<ModifierGroup>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));

        modelBuilder.Entity<Transaction>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<TransactionItem>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<Payment>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<Return>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<CashierSession>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<PettyCashExpense>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<AuditLog>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));

        modelBuilder.Entity<InventoryStock>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<StockLocation>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<UserStockLocationAccess>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<PosRegister>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<PosDevice>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<PosDevicePairingCode>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<StockLedger>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<StockReconciliationRun>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<StockReconciliationItem>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<StockOpname>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<StockOpnameItem>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<StockTransfer>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<StockTransferItem>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<ChannelStockPolicy>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<ProductVariant>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<ProductRecipe>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<ProductBatch>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<ProductSerial>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<ProductAttributeValue>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<ModifierOption>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<ModifierGroupProduct>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));

        modelBuilder.Entity<PurchaseOrder>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<PurchaseOrderItem>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<ReceivingNote>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<ReceivingNoteItem>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<SupplierDebt>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<SupplierPayment>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<SupplierReturn>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<SupplierReturnItem>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));

        modelBuilder.Entity<Consignment>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<ConsignmentItem>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<ConsignmentSale>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<ConsignmentSettlement>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<ConsignmentReturn>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<ConsignmentReturnItem>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));

        modelBuilder.Entity<ChannelAccount>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<ChannelSettlement>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<ChannelSettlementItem>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<GoBizIntegration>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<GoBizOAuthState>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<GoBizClientConfig>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<GoBizDirectIntegration>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<GoBizProductMapping>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<GoBizOrderInbox>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<IntegrationLog>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<RefreshToken>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<IdempotencyRecord>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<OutboxMessage>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));

        modelBuilder.Entity<TaxRule>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<ServiceChargeRule>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<PromoCampaign>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<PromoCampaignTarget>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<Voucher>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));
        modelBuilder.Entity<VoucherRedemption>().HasQueryFilter(e => SystemContext || (CurrentBusinessId.HasValue && e.BusinessId == CurrentBusinessId));

        // Global tables (tidak tenant-owned): roles, permissions,
        // role_permissions. Business adalah root tenant dan difilter lewat Id.

        // FK ke businesses dibuat EF via konvensi (nav `Business` + `BusinessId`);
        // hapus business tidak boleh menghapus rantai data tenant.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var nav = entityType.FindNavigation("Business");
            if (nav?.ForeignKey != null)
                nav.ForeignKey.DeleteBehavior = DeleteBehavior.Restrict;
        }

        base.OnModelCreating(modelBuilder);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ValidateTenantChanges();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ValidateTenantChanges();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ValidateTenantChanges();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override int SaveChanges()
    {
        ValidateTenantChanges();
        return base.SaveChanges();
    }

    private void ValidateTenantChanges()
    {
        if (SystemContext)
            return;

        var currentBusinessId = CurrentBusinessId;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State == EntityState.Unchanged || entry.State == EntityState.Detached)
                continue;

            var businessIdProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "BusinessId");
            if (businessIdProperty is null)
                continue;

            if (!currentBusinessId.HasValue)
            {
                throw new InvalidOperationException("Business context wajib tersedia untuk operasi tenant.");
            }

            var currentValue = businessIdProperty.CurrentValue;
            if (entry.State == EntityState.Added &&
                (currentValue is null || (currentValue is Guid addedId && addedId == Guid.Empty)))
            {
                businessIdProperty.CurrentValue = currentBusinessId.Value;
                continue;
            }

            if (currentValue is not Guid value || value == Guid.Empty || value != currentBusinessId.Value)
            {
                throw new InvalidOperationException("Data tenant tidak sesuai dengan business context aktif.");
            }

            if (entry.State == EntityState.Modified && businessIdProperty.IsModified &&
                businessIdProperty.OriginalValue is Guid originalValue && originalValue != value)
                throw new InvalidOperationException("BusinessId data tenant tidak boleh diubah.");
        }
    }
}
