using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
using MorrusPOS.Infrastructure.Persistence;
using MorrusPOS.Infrastructure.Services;

namespace MorrusPOS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(config.GetConnectionString("DefaultConnection")));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped(typeof(IRepository<>), typeof(GenericRepository<>));

        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IInventoryService, InventoryService>();
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<ICashierSessionService, CashierSessionService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IPricingService, PricingService>();
        services.AddScoped<IPricingAdminService, PricingAdminService>();
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

        // Fase 8 — Dashboard & Laporan
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IReportService, ReportService>();

        return services;
    }
}
