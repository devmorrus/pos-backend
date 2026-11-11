using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Auth;
using MorrusPOS.Application.Features.Users;
using MorrusPOS.Application.Features.Products;
using MorrusPOS.Application.Features.Transactions;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Application.Features.Suppliers;
using MorrusPOS.Application.Features.Consignments;
using MorrusPOS.Application.Features.Dashboard;
using MorrusPOS.Application.Features.Reports;
using MorrusPOS.Application.Features.Channels;
using MorrusPOS.Application.Features.Pricing;
using MorrusPOS.Application.Features.Customers;
using MorrusPOS.Application.Features.Accounting;
using MorrusPOS.Application.Features.Capabilities;
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Options;
using MorrusPOS.Infrastructure.Services;
using MorrusPOS.Infrastructure.Monitoring;
using MorrusPOS.Infrastructure.Outbox;
using MorrusPOS.Application.Common.Events;

namespace MorrusPOS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddScoped<TenantDbConnectionInterceptor>();
        services.AddDbContext<AppDbContext>((serviceProvider, options) =>
            options.UseNpgsql(config.GetConnectionString("DefaultConnection")
                    ?? throw new InvalidOperationException("DefaultConnection wajib dikonfigurasi."))
                .AddInterceptors(serviceProvider.GetRequiredService<TenantDbConnectionInterceptor>()));
        services.AddDbContext<SystemDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("SystemConnection")
                ?? throw new InvalidOperationException("SystemConnection wajib dikonfigurasi dengan role morrus_system.")));
        services.AddOptions<OutboxOptions>()
            .Bind(config.GetSection(OutboxOptions.SectionName))
            .Validate(options => options.PollIntervalMilliseconds >= 100, "Outbox poll interval minimal 100ms.")
            .Validate(options => options.BatchSize is >= 1 and <= 200, "Outbox batch size harus 1-200.")
            .Validate(options => options.MaxAttempts is >= 1 and <= 20, "Outbox max attempts harus 1-20.")
            .Validate(options => options.BaseRetrySeconds >= 1 && options.MaxRetrySeconds >= options.BaseRetrySeconds,
                "Outbox retry interval tidak valid.")
            .Validate(options => options.HandlerTimeoutSeconds >= 1 && options.LeaseSeconds > options.HandlerTimeoutSeconds,
                "Outbox lease harus lebih panjang dari handler timeout.")
            .Validate(options => options.ProcessedRetentionDays >= 1, "Outbox retention minimal satu hari.")
            .Validate(options => options.MaxPayloadBytes is >= 1024 and <= 262_144,
                "Outbox payload limit harus 1KB-256KB.")
            .ValidateOnStart();

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));

        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProductCatalogMasterService, ProductCatalogMasterService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<IIdempotencyService, IdempotencyService>();
        services.AddScoped<IStockReconciliationService, StockReconciliationService>();
        services.AddScoped<IOnlineStockAvailabilityService, OnlineStockAvailabilityService>();
        services.AddScoped<IBufferStockService, BufferStockService>();
        services.AddScoped<ICashierSessionService, CashierSessionService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<IPricingAdminService, PricingAdminService>();
        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IStockOpnameService, StockOpnameService>();
        services.AddScoped<IStockTransferService, StockTransferService>();

        // Fase 5 — Supplier, PO & Utang Usaha
        services.AddScoped<ISupplierService, SupplierService>();
        services.AddScoped<IPurchaseOrderService, PurchaseOrderService>();
        services.AddScoped<ISupplierDebtService, SupplierDebtService>();
        services.AddScoped<ISupplierReturnService, SupplierReturnService>();

        // Fase 6 — Barang Titipan / Konsinyasi
        services.AddScoped<IConsignmentService, ConsignmentService>();
        services.AddScoped<IConsignmentSettlementService, ConsignmentSettlementService>();
        services.AddScoped<IConsignmentReturnService, ConsignmentReturnService>();
        services.AddScoped<IChannelAccountService, ChannelAccountService>();
        services.AddScoped<IChannelSettlementService, ChannelSettlementService>();
        services.AddMemoryCache();
        services.AddDataProtection();
        services.Configure<AccountingOptions>(config.GetSection(AccountingOptions.SectionName));
        services.AddOptions<GoBizOptions>()
            .Bind(config.GetSection(GoBizOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<GoBizOptions>, GoBizOptionsValidator>();
        services.AddScoped<IGoBizConfigProvider, GoBizConfigProvider>();
        services.AddScoped<IGoBizClientConfigService, GoBizClientConfigService>();
        services.AddScoped<IGoBizOAuthStateStore, GoBizOAuthStateStore>();
        services.AddScoped<IGoBizAuthService, GoBizAuthService>();
        services.AddHttpClient<IGoBizOAuthService, GoBizOAuthService>();
        services.AddHttpClient<IGoBizDirectAuthService, GoBizDirectAuthService>();
        services.AddHttpClient<IGoBizApiClient, GoBizApiClient>();
        services.AddScoped<IGoBizDirectIntegrationService, GoBizDirectIntegrationService>();
        services.AddScoped<IGoBizOrderWebhookService, GoBizOrderWebhookService>();
        services.AddScoped<IQueueMonitor, GoBizInboxQueueMonitor>();
        services.AddScoped<IQueueMonitor, OutboxQueueMonitor>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<IOutboxStore, OutboxStore>();
        services.AddScoped<IStockEventPublisher, StockEventPublisher>();

        // Fase 8 — Dashboard & Laporan
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportService, ReportService>();
        services.AddScoped<IChartOfAccountService, ChartOfAccountService>();
        services.AddScoped<ICashFlowService, CashFlowService>();
        services.AddScoped<ICashFlowPostingService, CashFlowPostingService>();
        services.AddScoped<IAccountingIntegrationService, AccountingIntegrationService>();
        services.AddScoped<IJournalPostingService, JournalPostingService>();
        services.AddScoped<IAccountingAccountMappingService, AccountingAccountMappingService>();
        services.AddScoped<IAccountingReconciliationService, AccountingReconciliationService>();
        services.AddScoped<ICapabilityEvaluator, CapabilityEvaluator>();
        services.AddScoped<ICapabilityAdminService, CapabilityAdminService>();

        return services;
    }
}
