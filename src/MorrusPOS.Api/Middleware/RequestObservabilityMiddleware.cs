using System.Diagnostics;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Routing;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Common.Observability;

namespace MorrusPOS.Api.Middleware;

public sealed partial class RequestObservabilityMiddleware
{
    private const string RequestIdHeader = "X-Request-ID";
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestObservabilityMiddleware> _logger;

    public RequestObservabilityMiddleware(RequestDelegate next, ILogger<RequestObservabilityMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, ICurrentUserService currentUser)
    {
        var supplied = context.Request.Headers[RequestIdHeader].ToString();
        var requestId = IsSafeRequestId(supplied) ? supplied : Guid.NewGuid().ToString("N");
        context.TraceIdentifier = requestId;
        context.Response.Headers[RequestIdHeader] = requestId;
        var activity = Activity.Current;
        var stopwatch = Stopwatch.StartNew();

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["RequestId"] = requestId,
            ["TraceId"] = activity?.TraceId.ToString(),
            ["SpanId"] = activity?.SpanId.ToString(),
        }))
        {
            try
            {
                await _next(context);
            }
            finally
            {
                stopwatch.Stop();
                var route = (context.GetEndpoint() as RouteEndpoint)?.RoutePattern.RawText
                    ?? context.Request.Path.Value
                    ?? "unknown";
                activity?.SetTag("morrus.business.id", currentUser.BusinessId);
                activity?.SetTag("morrus.outlet.id", currentUser.OutletId);
                activity?.SetTag("morrus.request.id", requestId);

                if (string.Equals(route, "/api/transactions/checkout", StringComparison.OrdinalIgnoreCase))
                {
                    var outcome = context.Response.StatusCode < 400
                        ? "success"
                        : context.Response.StatusCode < 500 ? "business_rejected" : "system_error";
                    MorrusTelemetry.RecordCheckout(outcome, stopwatch.Elapsed.TotalMilliseconds);
                }

                if (!context.Request.Path.StartsWithSegments("/health/live"))
                {
                    _logger.Log(
                        context.Response.StatusCode >= 500 ? LogLevel.Error : LogLevel.Information,
                        new EventId(1000, "HttpRequestCompleted"),
                        "HttpRequestCompleted Method={Method} Route={Route} StatusCode={StatusCode} DurationMs={DurationMs} BusinessId={BusinessId} OutletId={OutletId} Role={Role}",
                        context.Request.Method,
                        route,
                        context.Response.StatusCode,
                        Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2),
                        currentUser.BusinessId,
                        currentUser.OutletId,
                        currentUser.Role);
                }
            }
        }
    }

    private static bool IsSafeRequestId(string value)
        => value.Length is > 0 and <= 64 && SafeRequestIdRegex().IsMatch(value);

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex SafeRequestIdRegex();
}
