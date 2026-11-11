using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Api.Middleware;
using MorrusPOS.Api.Security;
using MorrusPOS.Api.Hubs;
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
using MorrusPOS.Application.Features.Accounting;
using MorrusPOS.Application.Features.Accounting.Validators;
using MorrusPOS.Application.Features.Reports;
using MorrusPOS.Infrastructure;
using MorrusPOS.Infrastructure.Options;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using MorrusPOS.Api.Health;
using MorrusPOS.Api.Observability;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MorrusPOS.Api.Monitoring;
using MorrusPOS.Application.Common.Monitoring;
using MorrusPOS.Application.Common.Events;
using MorrusPOS.Api.Outbox;

var builder = WebApplication.CreateBuilder(args);
var deviceEnforcement = builder.Configuration["Pos:DeviceEnforcement"]
    ?? PosDeviceMiddleware.EnforcementReportOnly;
if (deviceEnforcement is not PosDeviceMiddleware.EnforcementReportOnly
    and not PosDeviceMiddleware.EnforcementRequired)
    throw new InvalidOperationException("Pos:DeviceEnforcement harus report_only atau required.");
builder.AddMorrusObservability();

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
builder.Services.AddScoped<IValidator<ReceiveStockTransferRequest>, ReceiveStockTransferRequestValidator>();
builder.Services.AddScoped<IValidator<UpsertBufferStockRequest>, UpsertBufferStockRequestValidator>();
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
builder.Services.AddScoped<IValidator<CreateChartOfAccountRequest>, CreateChartOfAccountRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateChartOfAccountRequest>, UpdateChartOfAccountRequestValidator>();
builder.Services.AddScoped<IValidator<UpdateChartOfAccountStatusRequest>, UpdateChartOfAccountStatusRequestValidator>();
builder.Services.AddScoped<IValidator<CreateBusinessIncomeRequest>, CreateBusinessIncomeRequestValidator>();
builder.Services.AddScoped<IValidator<CreateBusinessOutcomeRequest>, CreateBusinessOutcomeRequestValidator>();
builder.Services.AddScoped<IValidator<AccountingCashFlowReportFilters>, AccountingCashFlowReportFiltersValidator>();
builder.Services.AddScoped<IValidator<AccountingProfitLossReportFilters>, AccountingProfitLossReportFiltersValidator>();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUserService>();
builder.Services.AddScoped<ICurrentUserService>(sp => sp.GetRequiredService<CurrentUserService>());
builder.Services.AddScoped<ITenantContextSetter>(sp => sp.GetRequiredService<CurrentUserService>());
builder.Services.AddScoped<PosDeviceContext>();
builder.Services.AddScoped<IPosDeviceContext>(sp => sp.GetRequiredService<PosDeviceContext>());
builder.Services.AddScoped<IOutboxEventHandler, SignalRStockUpdatedEventHandler>();
builder.Services.AddScoped<IOutboxEventHandler, SignalRStockUpdatedV2EventHandler>();
builder.Services.AddHostedService<OutboxDispatcherBackgroundService>();
builder.Services.Configure<QueueMonitoringOptions>(
    builder.Configuration.GetSection(QueueMonitoringOptions.SectionName));
builder.Services.AddSingleton<QueueMonitoringState>();
builder.Services.AddHostedService<QueueMonitoringBackgroundService>();
builder.Services.AddHealthChecks()
    .AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<DatabaseReadinessHealthCheck>("postgresql", tags: ["ready"])
    .AddCheck<QueueBacklogHealthCheck>("queues", tags: ["queue"]);
builder.Services.AddSingleton<IHealthCheckPublisher, ReadinessHealthCheckPublisher>();
builder.Services.Configure<HealthCheckPublisherOptions>(options =>
{
    options.Predicate = registration => registration.Tags.Contains("ready");
    options.Delay = TimeSpan.FromSeconds(5);
    options.Period = TimeSpan.FromSeconds(30);
    options.Timeout = TimeSpan.FromSeconds(10);
});

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
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(accessToken) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hub/notifications"))
                    context.Token = accessToken;

                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// Custom Permission-based Authorization (caching role permissions for sub-millisecond scalability)
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
builder.Services.AddScoped<IAuthorizationHandler, CapabilityAuthorizationHandler>();
builder.Services.AddSingleton<Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler,
    CapabilityAuthorizationResultHandler>();
builder.Services.AddScoped<CapabilityOutletActionFilter>();
builder.Services.AddScoped<StockLocationAccessResolver>();

// ---- Controllers & Swagger ----
builder.Services.AddControllers(options =>
{
    options.Filters.Add<ValidationFilter>();
    options.Filters.AddService<CapabilityOutletActionFilter>();
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

using (var scope = app.Services.CreateScope())
{
    var goBizOptions = scope.ServiceProvider.GetRequiredService<IOptions<GoBizOptions>>().Value;
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    int dbConfigCount = 0;
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<MorrusPOS.Infrastructure.Persistence.SystemDbContext>();
        dbConfigCount = db.Set<MorrusPOS.Domain.Entities.GoBizClientConfig>().Count(x => x.IsActive);
    }
    catch { /* DB belum migrate saat startup awal */ }
    logger.LogInformation("GoBiz configured. DbConfigs={DbConfigs}. FallbackEnv={Env}. FallbackClientId={HasClientId}. FallbackSecret={HasSecret}. RedirectUri={RedirectUri}",
        dbConfigCount,
        goBizOptions.Environment,
        !string.IsNullOrWhiteSpace(goBizOptions.ClientId),
        !string.IsNullOrWhiteSpace(goBizOptions.ClientSecret),
        goBizOptions.RedirectUri);
}

app.Logger.LogInformation(
    "ApplicationStarting Environment={Environment} ContentRootPath={ContentRootPath} UploadsPath={UploadsPath} UploadsDirectoryExists={UploadsDirectoryExists}",
    app.Environment.EnvironmentName,
    app.Environment.ContentRootPath,
    Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads"),
    Directory.Exists(Path.Combine(app.Environment.ContentRootPath, "wwwroot", "uploads")));

// Auto-migrate database on startup
using (var scope = app.Services.CreateScope())
{
    var migrationConnection = builder.Configuration.GetConnectionString("MigrationConnection")
        ?? throw new InvalidOperationException("MigrationConnection wajib dikonfigurasi dengan role morrus_migration.");
    var migrationOptions = new DbContextOptionsBuilder<MorrusPOS.Infrastructure.Persistence.AppDbContext>()
        .UseNpgsql(migrationConnection)
        .Options;
    await using var context = new MorrusPOS.Infrastructure.Persistence.AppDbContext(migrationOptions);
    await context.Database.MigrateAsync();

    var runtimeDb = scope.ServiceProvider.GetRequiredService<MorrusPOS.Infrastructure.Persistence.AppDbContext>();
    await runtimeDb.Database.OpenConnectionAsync();
    await using var roleCommand = runtimeDb.Database.GetDbConnection().CreateCommand();
    roleCommand.CommandText = "select rolsuper, rolbypassrls from pg_roles where rolname = current_user;";
    await using var roleReader = await roleCommand.ExecuteReaderAsync();
    if (!await roleReader.ReadAsync() || roleReader.GetBoolean(0) || roleReader.GetBoolean(1))
        throw new InvalidOperationException("Runtime database role must not be superuser or bypass RLS.");
    await roleReader.DisposeAsync();

    await using var isolationCommand = runtimeDb.Database.GetDbConnection().CreateCommand();
    isolationCommand.CommandText = """
        select count(*)
        from pg_class c
        join pg_namespace n on n.oid=c.relnamespace
        where n.nspname='public' and c.relkind='r'
          and (
            (exists (select 1 from pg_attribute a where a.attrelid=c.oid and a.attname='BusinessId' and not a.attisdropped)
             and (not c.relrowsecurity or not c.relforcerowsecurity
                  or not exists (select 1 from pg_policy p where p.polrelid=c.oid and p.polname='tenant_isolation')))
            or (c.relname='businesses'
                and (not c.relrowsecurity or not c.relforcerowsecurity
                     or not exists (select 1 from pg_policy p where p.polrelid=c.oid and p.polname='tenant_isolation')))
          );
        """;
    var unprotectedTenantTables = Convert.ToInt32(await isolationCommand.ExecuteScalarAsync());
    if (unprotectedTenantTables > 0)
        throw new InvalidOperationException($"Database tenant isolation is incomplete for {unprotectedTenantTables} table(s).");
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

app.UseMiddleware<RequestObservabilityMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<SubscriptionVerificationMiddleware>();

// Guard multi-tenant: pastikan user hanya bisa akses data outlet-nya sendiri
// (kecuali Owner, yang outlet_id-nya null di JWT claim)
app.UseMiddleware<OutletTenantMiddleware>();
app.UseMiddleware<PosDeviceMiddleware>();

app.MapControllers();
app.MapHub<NotificationHub>("/hub/notifications");
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("live"),
    ResponseWriter = HealthResponseWriter.WriteAsync,
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteAsync,
});
app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.WriteAsync,
});
app.MapHealthChecks("/health/queues", new HealthCheckOptions
{
    Predicate = registration => registration.Tags.Contains("queue"),
    ResponseWriter = QueueHealthResponseWriter.WriteAsync,
    ResultStatusCodes =
    {
        [HealthStatus.Healthy] = StatusCodes.Status200OK,
        [HealthStatus.Degraded] = StatusCodes.Status200OK,
        [HealthStatus.Unhealthy] = StatusCodes.Status503ServiceUnavailable,
    },
});

app.Run();

public partial class Program { }
