using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MorrusPOS.Application.Common.Events;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Common.Monitoring;
using MorrusPOS.Domain.Entities;
using MorrusPOS.Infrastructure.Outbox;
using MorrusPOS.Infrastructure.Persistence;
using NSubstitute;

namespace MorrusPOS.UnitTests;

public class OutboxTests
{
    [Theory]
    [InlineData(1, 0, 5)]
    [InlineData(2, 0, 10)]
    [InlineData(4, 0, 40)]
    [InlineData(20, 0, 900)]
    [InlineData(1, 0.2, 6)]
    public void RetrySchedule_IsExponentialCappedAndJittered(int attempt, double jitter, double expectedSeconds)
    {
        var options = new OutboxOptions { BaseRetrySeconds = 5, MaxRetrySeconds = 900 };

        OutboxRetrySchedule.Calculate(attempt, options, jitter).TotalSeconds.Should().Be(expectedSeconds);
    }

    [Fact]
    public void QueuePolicies_KeepGoBizReportOnlyAndEvaluateOutbox()
    {
        var options = new QueueMonitoringOptions
        {
            Queues = new Dictionary<string, QueueMonitoringPolicy>(StringComparer.OrdinalIgnoreCase)
            {
                ["gobiz_order_inbox"] = new() { EvaluateBacklog = false },
                ["outbox"] = new()
                {
                    EvaluateBacklog = true,
                    WarningFailedCount = 1,
                    CriticalFailedCount = 50,
                    CriticalDeadLetterCount = 1,
                },
            },
        };
        var now = DateTime.UtcNow;

        var goBiz = new QueueSnapshot("gobiz_order_inbox", 1000, 0, 100, 2, now.AddHours(-1), now, null, now);
        var retryingOutbox = goBiz with
        {
            QueueName = "outbox",
            PendingCount = 0,
            FailedCount = 1,
            DeadLetterCount = 0,
            OldestPendingAtUtc = now,
        };
        var deadOutbox = retryingOutbox with { FailedCount = 0, DeadLetterCount = 1 };

        QueueHealthEvaluator.Evaluate(goBiz, options).Should().Be(QueueHealthLevel.Healthy);
        QueueHealthEvaluator.Evaluate(retryingOutbox, options).Should().Be(QueueHealthLevel.Degraded);
        QueueHealthEvaluator.Evaluate(deadOutbox, options).Should().Be(QueueHealthLevel.Unhealthy);
    }

    [Fact]
    public async Task StockPublisher_UsesAbsoluteProjectionAndZeroForMissingRows()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new AppDbContext(options, TestTenant.CurrentUser);
        var outletId = Guid.NewGuid();
        var productWithStock = Guid.NewGuid();
        var missingProduct = Guid.NewGuid();
        db.InventoryStocks.Add(new InventoryStock
        {
            Id = Guid.NewGuid(),
            OutletId = outletId,
            ProductId = productWithStock,
            QtyOnHand = 17,
        });
        await db.SaveChangesAsync();
        var writer = Substitute.For<IOutboxWriter>();
        var publisher = new StockEventPublisher(db, TestTenant.CurrentUser, writer);

        await publisher.EnqueueCurrentStockAsync(
            outletId,
            [new StockEventKey(productWithStock), new StockEventKey(missingProduct)],
            "stock:test");

        await writer.Received(1).EnqueueAsync(
            Arg.Is<OutboxWriteRequest>(request => request != null && PayloadMatches(request.PayloadJson, productWithStock, missingProduct)),
            Arg.Any<CancellationToken>());
    }

    private static bool PayloadMatches(string json, Guid productWithStock, Guid missingProduct)
    {
        var payload = JsonSerializer.Deserialize<StockUpdatedEventV1>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        return payload is not null &&
               payload.Items.Single(x => x.ProductId == productWithStock).QtyOnHand == 17 &&
               payload.Items.Single(x => x.ProductId == missingProduct).QtyOnHand == 0;
    }
}
