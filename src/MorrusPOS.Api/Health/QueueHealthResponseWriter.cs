using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MorrusPOS.Api.Health;

public static class QueueHealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var checks = report.Entries.Select(entry => new
        {
            name = entry.Key,
            status = entry.Value.Status.ToString(),
            description = entry.Value.Description,
            data = entry.Value.Data,
        });
        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            sampledAtUtc = DateTime.UtcNow,
            checks,
        }));
    }
}
