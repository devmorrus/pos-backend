using System.Diagnostics.Metrics;
using MorrusPOS.Application.Common.Observability;
using MorrusPOS.Application.Common.Monitoring;

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

    [Fact]
    public void QueueMetrics_DoNotExposeTenantOrOrderIdentifiers()
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
        var now = DateTime.UtcNow;

        MorrusTelemetry.RecordQueueSnapshot(
            new QueueSnapshot("gobiz_order_inbox", 3, 1, 0, 0, now.AddSeconds(-10), now, null, now),
            false,
            5);
        listener.RecordObservableInstruments();

        var depths = measurements.Where(measurement => measurement.Instrument == "morrus.queue.depth").ToList();
        Assert.Equal(4, depths.Count);
        Assert.All(depths, depth =>
        {
            Assert.Contains("queue.status", depth.Tags);
            Assert.Contains("queue.name", depth.Tags);
            Assert.DoesNotContain("business.id", depth.Tags);
            Assert.DoesNotContain("outlet.id", depth.Tags);
            Assert.DoesNotContain("order.id", depth.Tags);
        });
    }

    [Fact]
    public void OutboxMetrics_UseOnlyBoundedDeliveryTags()
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

        MorrusTelemetry.RecordOutboxDelivery("pos.stock.updated", "retry", "OUTBOX_HANDLER_TIMEOUT", 50);

        var delivery = Assert.Single(measurements, measurement => measurement.Instrument == "morrus.outbox.deliveries");
        Assert.Contains("event.type", delivery.Tags);
        Assert.Contains("outcome", delivery.Tags);
        Assert.Contains("error.code", delivery.Tags);
        Assert.DoesNotContain("business.id", delivery.Tags);
        Assert.DoesNotContain("outlet.id", delivery.Tags);
        Assert.DoesNotContain("event.id", delivery.Tags);
    }
}
