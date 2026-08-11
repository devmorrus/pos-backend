using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Api.Middleware;
using MorrusPOS.Api.Security;
using MorrusPOS.Api.Hubs;
using MorrusPOS.Api.Services;
using FluentValidation;
using MorrusPOS.Api.Filters;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Auth;
using MorrusPOS.Application.Features.Auth.Validators;
using MorrusPOS.Application.Features.Users;
using MorrusPOS.Application.Features.Users.Validators;
using MorrusPOS.Application.Features.Products;
using MorrusPOS.Application.Features.Products.Validators;
using MorrusPOS.Application.Features.Transactions;
using MorrusPOS.Application.Features.Transactions.Validators;
using MorrusPOS.Application.Features.Stock;
using MorrusPOS.Application.Features.Stock.Validators;
using MorrusPOS.Application.Features.Suppliers;
using MorrusPOS.Application.Features.Suppliers.Validators;
using MorrusPOS.Application.Features.Consignments;
using MorrusPOS.Application.Features.Consignments.Validators;
using MorrusPOS.Application.Features.Channels;
using MorrusPOS.Application.Features.Channels.Validators;
using MorrusPOS.Application.Features.Pricing;
using MorrusPOS.Application.Features.Pricing.Validators;
using MorrusPOS.Application.Features.Customers;
using MorrusPOS.Application.Features.Customers.Validators;
using MorrusPOS.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ---- Layer registration ----
builder.Services.AddInfrastructure(builder.Configuration);

// ---- FluentValidation Validators ----
builder.Services.AddScoped<IValidator<LoginRequest>, LoginRequestValidator>();
builder.Services.AddScoped<IValidator<CreateUserRequest>, CreateUserRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateUserRequest>, UpdateUserRequestValidator>();
builder.Services.AddScoped<IValidator<ChangePasswordRequest>, ChangePasswordRequestValidator>();
builder.Services.AddScoped<IValidator<CreateProductRequest>, CreateProductRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateProductRequest>, UpdateProductRequestValidator>();
builder.Services.AddScoped<IValidator<CreateCategoryRequest>, CreateCategoryRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateCategoryRequest>, UpdateCategoryRequestValidator>();
builder.Services.AddScoped<IValidator<OpenSessionRequest>, OpenSessionRequestValidator>();
builder.Services.AddScoped<IValidator<CloseSessionRequest>, CloseSessionRequestValidator>();
builder.Services.AddScoped<IValidator<CheckoutRequest>, CheckoutRequestValidator>();
builder.Services.AddScoped<IValidator<PricingPreviewRequest>, PricingPreviewRequestValidator>();
builder.Services.AddScoped<IValidator<VoidTransactionRequest>, VoidTransactionRequestValidator>();
builder.Services.AddScoped<IValidator<RefundTransactionRequest>, RefundTransactionRequestValidator>();
builder.Services.AddScoped<IValidator<CreateStockOpnameRequest>, CreateStockOpnameRequestValidator>();
builder.Services.AddScoped<IValidator<CreateStockTransferRequest>, CreateStockTransferRequestValidator>();
builder.Services.AddScoped<IValidator<CreateSupplierRequest>, CreateSupplierRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateSupplierRequest>, UpdateSupplierRequestValidator>();
builder.Services.AddScoped<IValidator<CreatePurchaseOrderRequest>, CreatePurchaseOrderRequestValidator>();
builder.Services.AddScoped<IValidator<UpdatePoStatusRequest>, UpdatePoStatusRequestValidator>();
builder.Services.AddScoped<IValidator<CreateSupplierPaymentRequest>, CreateSupplierPaymentRequestValidator>();
builder.Services.AddScoped<IValidator<CreateSupplierReturnRequest>, CreateSupplierReturnRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateSupplierReturnRequest>, UpdateSupplierReturnRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateSupplierReturnStatusRequest>, UpdateSupplierReturnStatusRequestValidator>();
builder.Services.AddScoped<IValidator<CreateConsignmentRequest>, CreateConsignmentRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateConsignmentStatusRequest>, UpdateConsignmentStatusRequestValidator>();
builder.Services.AddScoped<IValidator<CreateConsignmentSettlementRequest>, CreateConsignmentSettlementRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateConsignmentSettlementStatusRequest>, UpdateConsignmentSettlementStatusRequestValidator>();
builder.Services.AddScoped<IValidator<CreateConsignmentReturnRequest>, CreateConsignmentReturnRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateConsignmentReturnStatusRequest>, UpdateConsignmentReturnStatusRequestValidator>();
builder.Services.AddScoped<IValidator<CreateChannelAccountRequest>, CreateChannelAccountRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateChannelAccountRequest>, UpdateChannelAccountRequestValidator>();
builder.Services.AddScoped<IValidator<CreateChannelSettlementRequest>, CreateChannelSettlementRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateChannelSettlementRequest>, UpdateChannelSettlementRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateChannelSettlementStatusRequest>, UpdateChannelSettlementStatusRequestValidator>();
builder.Services.AddScoped<IValidator<CreateTaxRuleRequest>, CreateTaxRuleRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateTaxRuleRequest>, UpdateTaxRuleRequestValidator>();
builder.Services.AddScoped<IValidator<CreateServiceChargeRuleRequest>, CreateServiceChargeRuleRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateServiceChargeRuleRequest>, UpdateServiceChargeRuleRequestValidator>();
builder.Services.AddScoped<IValidator<CreatePromoCampaignRequest>, CreatePromoCampaignRequestValidator>();
builder.Services.AddScoped<IValidator<UpdatePromoCampaignRequest>, UpdatePromoCampaignRequestValidator>();
builder.Services.AddScoped<IValidator<CreateVoucherRequest>, CreateVoucherRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateVoucherRequest>, UpdateVoucherRequestValidator>();
builder.Services.AddScoped<IValidator<CreateCustomerRequest>, CreateCustomerRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateCustomerRequest>, UpdateCustomerRequestValidator>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IPosNotificationService, PosNotificationService>();

// ---- JWT Auth ----
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!))
        };
    });

builder.Services.AddAuthorization();

// Custom Permission-based Authorization (caching role permissions for sub-millisecond scalability)
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

// ---- Controllers & Swagger ----
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
});
builder.Services.AddSignalR();
builder.Services.AddEndpointsApiExplorer();
// Swashbuckle.AspNetCore v10+ (dipakai untuk .NET 10) pindah ke Microsoft.OpenApi v2,
// yang mengubah cara mendaftarkan security requirement — OpenApiSecurityScheme.Reference
// sudah dihapus, diganti OpenApiSecuritySchemeReference + delegate berbasis "document".
// Referensi: https://github.com/domaindrivendev/Swashbuckle.AspNetCore/blob/master/docs/migrating-to-v10.md
builder.Services.AddSwaggerGen(c =>
{
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
    });
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});

// ---- CORS (untuk React TS frontend) ----
var frontendUrls = (builder.Configuration["Frontend:Url"] ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(frontendUrls)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

Console.WriteLine($"[DIAGNOSTIC] Current Directory: {Directory.GetCurrentDirectory()}");
Console.WriteLine($"[DIAGNOSTIC] WebRootPath: {app.Environment.WebRootPath}");
Console.WriteLine($"[DIAGNOSTIC] ContentRootPath: {app.Environment.ContentRootPath}");
Console.WriteLine($"[DIAGNOSTIC] Uploads Directory Path: {Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads")}");
Console.WriteLine($"[DIAGNOSTIC] Uploads Folder Exists: {Directory.Exists(Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads"))}");

// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<MorrusPOS.Infrastructure.Persistence.AppDbContext>();
    await context.Database.MigrateAsync();
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Ensure wwwroot/uploads directory exists at runtime
var wwwrootPath = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
var uploadsPath = Path.Combine(wwwrootPath, "uploads");
if (!Directory.Exists(uploadsPath))
{
    Directory.CreateDirectory(uploadsPath);
}

// Serve static files from wwwroot/uploads folder under /uploads request path
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(uploadsPath),
    RequestPath = "/uploads"
});

app.UseCors("FrontendPolicy");

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<SubscriptionVerificationMiddleware>();

// Guard multi-tenant: pastikan user hanya bisa akses data outlet-nya sendiri
// (kecuali Owner, yang outlet_id-nya null di JWT claim)
app.UseMiddleware<OutletTenantMiddleware>();

app.MapControllers();
app.MapHub<NotificationHub>("/hub/notifications");

app.Run();

public partial class Program { }
