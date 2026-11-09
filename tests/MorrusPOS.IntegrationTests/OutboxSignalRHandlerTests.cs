using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using MorrusPOS.Api.Hubs;
using MorrusPOS.Api.Outbox;
using MorrusPOS.Application.Common.Events;

namespace MorrusPOS.IntegrationTests;

public class OutboxSignalRHandlerTests
{
    [Fact]
    public async Task Handler_SendsVersionedAbsoluteEnvelopeToTenantOutletGroup()
    {
        var businessId = Guid.NewGuid();
        var outletId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var proxy = new CapturingClientProxy();
        var hub = new FakeHubContext($"Business_{businessId}_Outlet_{outletId}", proxy);
        var handler = new SignalRStockUpdatedEventHandler(hub);
        var payload = JsonSerializer.Serialize(
            new StockUpdatedEventV1(outletId, [new StockLevelV1(productId, null, 12)]),
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var message = new OutboxDeliveryMessage(
            Guid.NewGuid(), 42, businessId, outletId, OutboxEventTypes.StockUpdated, 1,
            payload, null, 1, 8, Guid.NewGuid(), DateTime.UtcNow);

        await handler.HandleAsync(message);

        Assert.Equal("ReceiveStockUpdateV2", proxy.Method);
        Assert.True(EnvelopeIsAbsolute(Assert.Single(proxy.Arguments!), message.Id, 42, productId));
    }

    [Fact]
    public async Task Handler_RejectsMalformedPayloadPermanently()
    {
        var handler = new SignalRStockUpdatedEventHandler(new FakeHubContext("unused", new CapturingClientProxy()));
        var message = new OutboxDeliveryMessage(
            Guid.NewGuid(), 1, Guid.NewGuid(), Guid.NewGuid(), OutboxEventTypes.StockUpdated, 1,
            "not-json", null, 1, 8, Guid.NewGuid(), DateTime.UtcNow);

        var exception = await Assert.ThrowsAsync<OutboxPermanentFailureException>(() => handler.HandleAsync(message));

        Assert.Equal("OUTBOX_INVALID_PAYLOAD", exception.ErrorCode);
    }

    private static bool EnvelopeIsAbsolute(object? envelope, Guid eventId, long sequence, Guid productId)
    {
        if (envelope is null) return false;
        var json = JsonSerializer.Serialize(envelope);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        return root.GetProperty("EventId").GetGuid() == eventId &&
               root.GetProperty("Sequence").GetInt64() == sequence &&
               root.GetProperty("IsAbsolute").GetBoolean() &&
               root.GetProperty("Updates")[0].GetProperty("ProductId").GetGuid() == productId &&
               root.GetProperty("Updates")[0].GetProperty("Qty").GetDecimal() == 12;
    }

    private sealed class CapturingClientProxy : IClientProxy
    {
        public string? Method { get; private set; }
        public object?[]? Arguments { get; private set; }

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            Method = method;
            Arguments = args;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeHubContext : IHubContext<NotificationHub>
    {
        public FakeHubContext(string expectedGroup, IClientProxy proxy)
        {
            Clients = new FakeHubClients(expectedGroup, proxy);
        }

        public IHubClients Clients { get; }
        public IGroupManager Groups { get; } = new NoopGroupManager();
    }

    private sealed class FakeHubClients : IHubClients
    {
        private readonly string _expectedGroup;
        private readonly IClientProxy _proxy;

        public FakeHubClients(string expectedGroup, IClientProxy proxy)
        {
            _expectedGroup = expectedGroup;
            _proxy = proxy;
        }

        public IClientProxy Group(string groupName)
        {
            Assert.Equal(_expectedGroup, groupName);
            return _proxy;
        }

        public IClientProxy All => _proxy;
        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => _proxy;
        public IClientProxy Client(string connectionId) => _proxy;
        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => _proxy;
        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => _proxy;
        public IClientProxy Groups(IReadOnlyList<string> groupNames) => _proxy;
        public IClientProxy User(string userId) => _proxy;
        public IClientProxy Users(IReadOnlyList<string> userIds) => _proxy;
    }

    private sealed class NoopGroupManager : IGroupManager
    {
        public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
