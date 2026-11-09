using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MorrusPOS.Application.Common.Interfaces;

namespace MorrusPOS.IntegrationTests;

public class ObservabilityApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public ObservabilityApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task QueueHealth_ReturnsAggregateWithoutTenantOrPayloadData()
    {
        var response = await _client.GetAsync("/health/queues");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("BusinessId", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RawPayload", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GoBizOrderId", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GoBizQueueMonitor_ReturnsEmptySnapshotForCleanDatabase()
    {
        using var scope = _factory.Services.CreateScope();
        var monitor = scope.ServiceProvider.GetServices<IQueueMonitor>()
            .Single(item => item.QueueName == "gobiz_order_inbox");

        var snapshot = await monitor.GetSnapshotAsync();

        Assert.Equal("gobiz_order_inbox", snapshot.QueueName);
        Assert.Equal(0, snapshot.PendingCount);
        Assert.Equal(0, snapshot.FailedCount);
    }

    [Fact]
    public async Task OutboxQueueMonitor_IsRegisteredAndDoesNotExposePayload()
    {
        using var scope = _factory.Services.CreateScope();
        var monitor = scope.ServiceProvider.GetServices<IQueueMonitor>()
            .Single(item => item.QueueName == "outbox");

        var snapshot = await monitor.GetSnapshotAsync();

        Assert.Equal("outbox", snapshot.QueueName);
        var response = await _client.GetAsync("/health/queues");
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PayloadJson", body, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    [InlineData("/health")]
    public async Task HealthEndpoints_ReturnHealthy(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<HealthPayload>();
        Assert.Equal("Healthy", payload?.Status);
        Assert.True(response.Headers.Contains("X-Request-ID"));
    }

    [Fact]
    public async Task RequestId_IsPreserved_EvenWhenAuthorizationRejectsRequest()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/products");
        request.Headers.Add("X-Request-ID", "qa-request-123");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("qa-request-123", response.Headers.GetValues("X-Request-ID").Single());
    }

    private sealed record HealthPayload(string Status);
}
