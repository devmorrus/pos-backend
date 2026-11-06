namespace MorrusPOS.Api.Observability;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";
    public bool Enabled { get; set; }
    public string ServiceName { get; set; } = "morruspos-api";
    public string OtlpEndpoint { get; set; } = "http://otel-collector:4317";
    public double TraceSampleRatio { get; set; } = 1;
}
