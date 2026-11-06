using System.Reflection;
using MorrusPOS.Application.Common.Observability;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace MorrusPOS.Api.Observability;

public static class ObservabilityExtensions
{
    public static void AddMorrusObservability(this WebApplicationBuilder builder)
    {
        var options = builder.Configuration
            .GetSection(ObservabilityOptions.SectionName)
            .Get<ObservabilityOptions>() ?? new ObservabilityOptions();
        builder.Services.Configure<ObservabilityOptions>(
            builder.Configuration.GetSection(ObservabilityOptions.SectionName));

        builder.Logging.ClearProviders();
        builder.Logging.AddJsonConsole(console =>
        {
            console.IncludeScopes = true;
            console.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";
            console.UseUtcTimestamp = true;
        });

        if (!options.Enabled)
            return;

        var endpoint = new Uri(options.OtlpEndpoint);
        var version = Environment.GetEnvironmentVariable("SERVICE_VERSION")
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "unknown";
        var environment = builder.Environment.EnvironmentName;
        Action<ResourceBuilder> configureResource = resource => resource
            .AddService(options.ServiceName, serviceVersion: version, serviceInstanceId: Environment.MachineName)
            .AddAttributes([new KeyValuePair<string, object>("deployment.environment.name", environment)]);

        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeScopes = true;
            logging.IncludeFormattedMessage = true;
            logging.SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService(options.ServiceName, serviceVersion: version, serviceInstanceId: Environment.MachineName)
                .AddAttributes([new KeyValuePair<string, object>("deployment.environment.name", environment)]));
            logging.AddOtlpExporter(exporter => ConfigureExporter(exporter, endpoint));
        });

        builder.Services.AddOpenTelemetry()
            .ConfigureResource(configureResource)
            .WithMetrics(metrics => metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddRuntimeInstrumentation()
                .AddMeter(MorrusTelemetry.Name)
                .AddOtlpExporter(exporter => ConfigureExporter(exporter, endpoint)))
            .WithTracing(tracing => tracing
                .SetSampler(new ParentBasedSampler(new TraceIdRatioBasedSampler(
                    Math.Clamp(options.TraceSampleRatio, 0, 1))))
                .AddAspNetCoreInstrumentation(instrumentation =>
                    instrumentation.Filter = context => !context.Request.Path.StartsWithSegments("/health/live"))
                .AddHttpClientInstrumentation()
                .AddSource(MorrusTelemetry.Name)
                .AddSource("Npgsql")
                .AddOtlpExporter(exporter => ConfigureExporter(exporter, endpoint)));
    }

    private static void ConfigureExporter(OtlpExporterOptions exporter, Uri endpoint)
    {
        exporter.Endpoint = endpoint;
        exporter.Protocol = OtlpExportProtocol.Grpc;
    }
}
