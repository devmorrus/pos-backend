using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using MorrusPOS.Application.Common.Events;
using Npgsql;

namespace MorrusPOS.IntegrationTests;

public sealed class OutboxDispatcherApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public OutboxDispatcherApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HostedDispatcher_DeliversPendingSignalREvent()
    {
        using var client = _factory.CreateClient();
        var businessId = Guid.NewGuid();
        var outletId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(
            new StockUpdatedEventV1(outletId, [new StockLevelV1(productId, null, 7)]),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await using var admin = new NpgsqlConnection(
            IntegrationTestDatabase.RequireConnectionString("MORRUS_TENANCY_TEST_ADMIN"));
        await admin.OpenAsync();

        try
        {
            await using (var seed = admin.CreateCommand())
            {
                seed.CommandText = """
                    INSERT INTO businesses
                        ("Id","Name","Slug","Category","IndustryProfileId","SubscriptionStatus","TrialStartDate","TrialEndDate","CreatedAt","UpdatedAt")
                    VALUES (@business,'Outbox Dispatcher Test',@slug,'Test','ca100001-0000-4000-8000-000000000001','Active',now(),now()+interval '1 day',now(),now());
                    INSERT INTO outlets ("Id","BusinessId","Name","Code","IsActive","CreatedAt","UpdatedAt")
                    VALUES (@outlet,@business,'Dispatcher Outlet',@code,true,now(),now());
                    INSERT INTO outbox_messages
                        ("Id","BusinessId","OutletId","EventType","SchemaVersion","PayloadJson","Status",
                         "AttemptCount","MaxAttempts","OccurredAtUtc","AvailableAtUtc","ManualReplayCount")
                    VALUES (@event,@business,@outlet,'pos.stock.updated',1,@payload::jsonb,'pending',0,8,now(),now(),0);
                    """;
                seed.Parameters.AddWithValue("business", businessId);
                seed.Parameters.AddWithValue("outlet", outletId);
                seed.Parameters.AddWithValue("event", eventId);
                seed.Parameters.AddWithValue("payload", payload);
                seed.Parameters.AddWithValue("slug", $"dispatcher-{businessId:N}");
                seed.Parameters.AddWithValue("code", $"OD-{businessId:N}"[..20]);
                await seed.ExecuteNonQueryAsync();
            }

            string? status = null;
            for (var attempt = 0; attempt < 50 && status != "processed"; attempt++)
            {
                await Task.Delay(100);
                await using var statusCommand = admin.CreateCommand();
                statusCommand.CommandText = "select \"Status\" from outbox_messages where \"Id\"=@event;";
                statusCommand.Parameters.AddWithValue("event", eventId);
                status = (string?)await statusCommand.ExecuteScalarAsync();
            }

            Assert.Equal("processed", status);
        }
        finally
        {
            await IntegrationTestDatabase.CleanupBusinessAsync(businessId);
        }
    }
}
