using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace MorrusPOS.IntegrationTests;

public class GoBizControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public GoBizControllerTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["GoBiz:Environment"] = "Sandbox",
                    ["GoBiz:ClientId"] = "test-client-id",
                    ["GoBiz:ClientSecret"] = "test-client-secret",
                    ["GoBiz:RedirectUri"] = "https://localhost:7100/api/gobiz/callback",
                    ["GoBiz:AuthorizationUrl"] = "https://integration-goauth.gojekapi.com/oauth2/auth",
                    ["GoBiz:TokenUrl"] = "https://integration-goauth.gojekapi.com/oauth2/token"
                });
            });
        }).CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    [Fact]
    public async Task CallbackRoute_Should_BeReachable_And_RejectMissingParameters()
    {
        var response = await _client.GetAsync("/api/gobiz/callback");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
