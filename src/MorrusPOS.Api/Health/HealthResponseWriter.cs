using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace MorrusPOS.Api.Health;

public static class HealthResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
        }));
    }
}
