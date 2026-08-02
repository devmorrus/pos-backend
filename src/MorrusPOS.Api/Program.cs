using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using Microsoft.IdentityModel.Tokens;
using MorrusPOS.Api.Middleware;
using MorrusPOS.Api.Security;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// ---- Layer registration ----
builder.Services.AddInfrastructure(builder.Configuration);

// TODO: buat AddApplication() extension di project Application kalau nanti
// ada MediatR/FluentValidation pipeline yang perlu didaftarkan terpusat.

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

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
builder.Services.AddControllers();
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

app.Run();

public partial class Program { }
