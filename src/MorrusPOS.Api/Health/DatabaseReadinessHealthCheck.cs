using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using MorrusPOS.Application.Common.Observability;
using MorrusPOS.Infrastructure.Persistence;

namespace MorrusPOS.Api.Health;

public sealed class DatabaseReadinessHealthCheck : IHealthCheck
{
    private readonly AppDbContext _dbContext;

    public DatabaseReadinessHealthCheck(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            MorrusTelemetry.SetReadiness(canConnect);
            return canConnect
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("PostgreSQL tidak dapat diakses.");
        }
        catch (Exception exception)
        {
            MorrusTelemetry.SetReadiness(false);
            return HealthCheckResult.Unhealthy("PostgreSQL tidak dapat diakses.", exception);
        }
    }
}
