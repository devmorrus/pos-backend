using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Microsoft.IdentityModel.Tokens;
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
builder.Services.AddScoped<IValidator<VoidTransactionRequest>, VoidTransactionRequestValidator>();
builder.Services.AddScoped<IValidator<RefundTransactionRequest>, RefundTransactionRequestValidator>();
builder.Services.AddScoped<IValidator<CreateStockOpnameRequest>, CreateStockOpnameRequestValidator>();
builder.Services.AddScoped<IValidator<CreateStockTransferRequest>, CreateStockTransferRequestValidator>();
builder.Services.AddScoped<IValidator<CreateSupplierRequest>, CreateSupplierRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateSupplierRequest>, UpdateSupplierRequestValidator>();
builder.Services.AddScoped<IValidator<CreatePurchaseOrderRequest>, CreatePurchaseOrderRequestValidator>();
builder.Services.AddScoped<IValidator<UpdatePoStatusRequest>, UpdatePoStatusRequestValidator>();
builder.Services.AddScoped<IValidator<CreateSupplierPaymentRequest>, CreateSupplierPaymentRequestValidator>();
builder.Services.AddScoped<IValidator<CreateConsignmentRequest>, CreateConsignmentRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateConsignmentStatusRequest>, UpdateConsignmentStatusRequestValidator>();
builder.Services.AddScoped<IValidator<CreateConsignmentSettlementRequest>, CreateConsignmentSettlementRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateConsignmentSettlementStatusRequest>, UpdateConsignmentSettlementStatusRequestValidator>();

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
builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendPolicy", policy =>
    {
        policy.WithOrigins(builder.Configuration["Frontend:Url"] ?? "http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("FrontendPolicy");

app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// Guard multi-tenant: pastikan user hanya bisa akses data outlet-nya sendiri
// (kecuali Owner, yang outlet_id-nya null di JWT claim)
app.UseMiddleware<OutletTenantMiddleware>();

app.MapControllers();
app.MapHub<NotificationHub>("/hub/notifications");

app.Run();

public partial class Program { }
