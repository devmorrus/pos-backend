using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Auth;
using MorrusPOS.Application.Features.Users;
using MorrusPOS.Application.Features.Products;
using MorrusPOS.Application.Features.Transactions;
using MorrusPOS.Application.Features.Stock;
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
        services.AddScoped<IStockService, StockService>();
        services.AddScoped<ICashierSessionService, CashierSessionService>();
        services.AddScoped<ITransactionService, TransactionService>();
        services.AddScoped<IStockOpnameService, StockOpnameService>();
        services.AddScoped<IStockTransferService, StockTransferService>();

        return services;
    }
}
