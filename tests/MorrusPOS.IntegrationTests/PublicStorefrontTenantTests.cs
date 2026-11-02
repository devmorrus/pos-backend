using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net.Http.Json;
using MorrusPOS.Application.Features.Capabilities;

namespace MorrusPOS.IntegrationTests;

public class PublicStorefrontTenantTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public PublicStorefrontTenantTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task TenantSlugRoutes_Work_AndLegacyGlobalCategoriesRouteIsClosed()
    {
        var outlets = await _client.GetAsync("/api/public/morrus-demo/outlets");
        Assert.Equal(HttpStatusCode.OK, outlets.StatusCode);

        var categories = await _client.GetAsync("/api/public/morrus-demo/categories");
        Assert.Equal(HttpStatusCode.OK, categories.StatusCode);

        var legacyGlobalCategories = await _client.GetAsync("/api/public/categories");
        Assert.Equal(HttpStatusCode.NotFound, legacyGlobalCategories.StatusCode);

        var profiles = await _client.GetFromJsonAsync<List<IndustryProfileDto>>("/api/public/industry-profiles");
        Assert.Equal(6, profiles?.Count);
    }
}
