using System.Diagnostics.Metrics;
using MorrusPOS.Application.Common.Observability;

namespace MorrusPOS.UnitTests;

public class MorrusTelemetryTests
{
    [Fact]
    public void CheckoutMetric_UsesBoundedOperationalTags()
    {
        var measurements = new List<(string Instrument, string[] Tags)>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, currentListener) =>
            {
                if (instrument.Meter.Name == MorrusTelemetry.Name)
                    currentListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
            measurements.Add((instrument.Name, tags.ToArray().Select(tag => tag.Key).ToArray())));
        listener.Start();

        MorrusTelemetry.RecordCheckout("success", 25, "pos");

        var checkout = Assert.Single(measurements, measurement => measurement.Instrument == "morrus.checkout.attempts");
        Assert.Contains("outcome", checkout.Tags);
        Assert.Contains("channel", checkout.Tags);
        Assert.DoesNotContain("business.id", checkout.Tags);
        Assert.DoesNotContain("outlet.id", checkout.Tags);
        Assert.DoesNotContain("user.id", checkout.Tags);
    }
}
