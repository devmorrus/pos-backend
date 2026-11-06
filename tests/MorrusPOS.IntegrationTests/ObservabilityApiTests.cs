using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace MorrusPOS.IntegrationTests;

public class ObservabilityApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public ObservabilityApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
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
