using Microsoft.Extensions.Diagnostics.HealthChecks;
using MorrusPOS.Application.Common.Observability;

namespace MorrusPOS.Api.Health;

public sealed class ReadinessHealthCheckPublisher : IHealthCheckPublisher
{
    public Task PublishAsync(HealthReport report, CancellationToken cancellationToken)
    {
        MorrusTelemetry.SetReadiness(report.Status == HealthStatus.Healthy);
        return Task.CompletedTask;
    }
}
