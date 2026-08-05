using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MorrusPOS.Application.Common.Interfaces;
using Xunit;

namespace MorrusPOS.IntegrationTests;

public class TestDashboard : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public TestDashboard(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RunDashboardController_And_PrintResponseOrException()
    {
        using var scope = _factory.Services.CreateScope();
        var jwtService = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();

        // Generate owner token
        var userId = Guid.Parse("a4f78de1-8a9d-4e96-857e-399fa5b5f25a");
        var token = jwtService.GenerateAccessToken(userId, null, "Owner");

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Act
        var response = await _client.GetAsync("/api/dashboard/summary?startDate=2026-07-06&endDate=2026-08-05");
        
        var content = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"HTTP_ERROR_{response.StatusCode}: {content}");
        }
    }
}
