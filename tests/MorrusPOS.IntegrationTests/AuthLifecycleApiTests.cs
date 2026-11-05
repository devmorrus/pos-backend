using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using MorrusPOS.Application.Features.Auth;

namespace MorrusPOS.IntegrationTests;

public class AuthLifecycleApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public AuthLifecycleApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RefreshRotation_AndRevoke_InvalidatePreviousTokens()
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        Guid? businessId = null;

        try
        {
            var registration = await _client.PostAsJsonAsync("/api/auth/register-owner", new RegisterOwnerRequest(
                new RegisterOwnerOwnerDetails("Auth Test Owner", $"auth-{suffix}@example.test", "Password123!"),
                new RegisterOwnerBusinessDetails($"Auth Test {suffix}", "Retail Umum", null, "retail"),
                "Free Trial",
                new RegisterOwnerOutletDetails("Outlet Utama", $"AUT-{suffix}", null, null)));

            registration.StatusCode.Should().Be(HttpStatusCode.OK);
            var initial = await registration.Content.ReadFromJsonAsync<LoginResponse>();
            initial.Should().NotBeNull();
            businessId = initial!.BusinessId;

            var refreshedResponse = await _client.PostAsJsonAsync(
                "/api/auth/refresh",
                new RefreshTokenRequest(initial.RefreshToken));
            refreshedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var refreshed = await refreshedResponse.Content.ReadFromJsonAsync<LoginResponse>();
            refreshed.Should().NotBeNull();
            refreshed!.RefreshToken.Should().NotBe(initial.RefreshToken);

            var replay = await _client.PostAsJsonAsync(
                "/api/auth/refresh",
                new RefreshTokenRequest(initial.RefreshToken));
            replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            var revoke = await _client.PostAsJsonAsync(
                "/api/auth/revoke",
                new RevokeTokenRequest(refreshed.RefreshToken));
            revoke.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var revokedRefresh = await _client.PostAsJsonAsync(
                "/api/auth/refresh",
                new RefreshTokenRequest(refreshed.RefreshToken));
            revokedRefresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        finally
        {
            if (businessId.HasValue)
                await IntegrationTestDatabase.CleanupBusinessAsync(businessId.Value);
        }
    }

    [Fact]
    public async Task Login_WithInvalidPayload_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email = "invalid", password = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
