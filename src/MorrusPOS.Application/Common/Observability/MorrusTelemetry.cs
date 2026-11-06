using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MorrusPOS.Application.Common.Observability;

public static class MorrusTelemetry
{
    public const string Name = "MorrusPOS";
    public static readonly ActivitySource ActivitySource = new(Name);
    public static readonly Meter Meter = new(Name);

    private static readonly Counter<long> Exceptions = Meter.CreateCounter<long>("morrus.exceptions");
    private static readonly Counter<long> CheckoutAttempts = Meter.CreateCounter<long>("morrus.checkout.attempts");
    private static readonly Histogram<double> CheckoutDuration = Meter.CreateHistogram<double>("morrus.checkout.duration", "ms");
    private static readonly Counter<long> StockIntegrityErrors = Meter.CreateCounter<long>("morrus.stock.integrity_errors");
    private static int _readiness = 1;

    static MorrusTelemetry()
    {
        Meter.CreateObservableGauge("morrus.api.info", () => 1);
        Meter.CreateObservableGauge("morrus.readiness", () => Volatile.Read(ref _readiness));
    }

    public static void SetReadiness(bool ready) => Volatile.Write(ref _readiness, ready ? 1 : 0);

    public static void RecordCheckout(string outcome, double durationMs, string channel = "pos")
    {
        var tags = new TagList { { "outcome", outcome }, { "channel", channel } };
        CheckoutAttempts.Add(1, tags);
        CheckoutDuration.Record(durationMs, tags);
    }

    public static void RecordException(string code, int statusCode)
    {
        var tags = new TagList { { "error.code", code }, { "http.response.status_code", statusCode } };
        Exceptions.Add(1, tags);
        if (code.StartsWith("STOCK_", StringComparison.Ordinal))
            StockIntegrityErrors.Add(1, tags);
    }
}
