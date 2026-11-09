using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using MorrusPOS.Api.Controllers;
using MorrusPOS.Application.Features.Auth;
using MorrusPOS.Application.Features.Transactions;

namespace MorrusPOS.IntegrationTests;

public class PosDevicePairingApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;
    private readonly WebApplicationFactory<Program> _factory;

    public PosDevicePairingApiTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task RequiredMode_RejectsCashierSessionWithoutDeviceHeaders()
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        Guid? businessId = null;
        await using var requiredFactory = _factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Pos:DeviceEnforcement", "required"));
        using var client = requiredFactory.CreateClient();

        try
        {
            var registration = await client.PostAsJsonAsync("/api/auth/register-owner", new RegisterOwnerRequest(
                new RegisterOwnerOwnerDetails("Required Test Owner", $"required-{suffix}@example.test", "Password123!"),
                new RegisterOwnerBusinessDetails($"Required Test {suffix}", "Retail Umum", null, "retail"),
                "Free Trial",
                new RegisterOwnerOutletDetails("Outlet Required", $"REQ-{suffix}", null, null)));
            registration.StatusCode.Should().Be(HttpStatusCode.OK);
            var login = await registration.Content.ReadFromJsonAsync<LoginResponse>();
            businessId = login!.BusinessId;
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);
            var outlet = (await client.GetFromJsonAsync<List<OutletResponse>>("/api/outlets"))!.Single();

            var response = await client.PostAsJsonAsync(
                "/api/cashier-sessions/open",
                new OpenSessionRequest(0, outlet.Id));

            response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        finally
        {
            if (businessId.HasValue)
                await IntegrationTestDatabase.CleanupBusinessAsync(businessId.Value);
        }
    }

    [Fact]
    public async Task PairingCode_IsSingleUse_AndRevokedDeviceCannotAuthenticate()
    {
        var suffix = Guid.NewGuid().ToString("N")[..10];
        Guid? businessId = null;

        try
        {
            var registration = await _client.PostAsJsonAsync("/api/auth/register-owner", new RegisterOwnerRequest(
                new RegisterOwnerOwnerDetails("Device Test Owner", $"device-{suffix}@example.test", "Password123!"),
                new RegisterOwnerBusinessDetails($"Device Test {suffix}", "Retail Umum", null, "retail"),
                "Free Trial",
                new RegisterOwnerOutletDetails("Outlet Device", $"DEV-{suffix}", null, null)));
            registration.StatusCode.Should().Be(HttpStatusCode.OK);
            var login = await registration.Content.ReadFromJsonAsync<LoginResponse>();
            businessId = login!.BusinessId;
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.AccessToken);

            var outlet = (await _client.GetFromJsonAsync<List<OutletResponse>>("/api/outlets"))!.Single();
            var registerResponse = await _client.PostAsJsonAsync("/api/pos-registers", new CreatePosRegisterRequest(
                outlet.Id, "REG-1", "Kasir 1"));
            registerResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var register = await registerResponse.Content.ReadFromJsonAsync<PosRegisterDto>();

            var codeResponse = await _client.PostAsJsonAsync(
                $"/api/pos-registers/{register!.Id}/pairing-codes", new { });
            codeResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var code = await codeResponse.Content.ReadFromJsonAsync<PairingCodeDto>();

            var installationId = Guid.NewGuid();
            var pairRequest = new PairPosDeviceRequest(
                code!.Code, installationId, "Chrome Kasir", "web", "test");
            var pairResponse = await _client.PostAsJsonAsync("/api/pos-devices/pair", pairRequest);
            pairResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var paired = await pairResponse.Content.ReadFromJsonAsync<PairedDeviceDto>();

            var replay = await _client.PostAsJsonAsync("/api/pos-devices/pair", pairRequest);
            replay.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            using var currentRequest = new HttpRequestMessage(HttpMethod.Get, "/api/pos-devices/current");
            currentRequest.Headers.Add("X-POS-Device-ID", paired!.DeviceId.ToString());
            currentRequest.Headers.Add("X-POS-Device-Token", paired.Token);
            var current = await _client.SendAsync(currentRequest);
            current.StatusCode.Should().Be(HttpStatusCode.OK);

            using var openRequest = new HttpRequestMessage(HttpMethod.Post, "/api/cashier-sessions/open")
            {
                Content = JsonContent.Create(new OpenSessionRequest(0, outlet.Id)),
            };
            openRequest.Headers.Add("X-POS-Device-ID", paired.DeviceId.ToString());
            openRequest.Headers.Add("X-POS-Device-Token", paired.Token);
            var openedResponse = await _client.SendAsync(openRequest);
            openedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var opened = await openedResponse.Content.ReadFromJsonAsync<CashierSessionDto>();
            opened!.RegisterId.Should().Be(register.Id);
            opened.DeviceId.Should().Be(paired.DeviceId);
            opened.StockLocationId.Should().Be(outlet.Id);

            using var closeRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/cashier-sessions/close/{opened.Id}")
            {
                Content = JsonContent.Create(new CloseSessionRequest(0)),
            };
            closeRequest.Headers.Add("X-POS-Device-ID", paired.DeviceId.ToString());
            closeRequest.Headers.Add("X-POS-Device-Token", paired.Token);
            (await _client.SendAsync(closeRequest)).StatusCode.Should().Be(HttpStatusCode.OK);

            using var revokeRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/pos-registers/{register.Id}/devices/{paired.DeviceId}/revoke")
            {
                Content = JsonContent.Create(new { }),
            };
            revokeRequest.Headers.Add("X-POS-Device-ID", paired.DeviceId.ToString());
            revokeRequest.Headers.Add("X-POS-Device-Token", paired.Token);
            var revoked = await _client.SendAsync(revokeRequest);
            revoked.StatusCode.Should().Be(HttpStatusCode.OK);

            using var rejectedRequest = new HttpRequestMessage(HttpMethod.Get, "/api/pos-devices/current");
            rejectedRequest.Headers.Add("X-POS-Device-ID", paired.DeviceId.ToString());
            rejectedRequest.Headers.Add("X-POS-Device-Token", paired.Token);
            var rejected = await _client.SendAsync(rejectedRequest);
            rejected.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
        finally
        {
            _client.DefaultRequestHeaders.Authorization = null;
            if (businessId.HasValue)
                await IntegrationTestDatabase.CleanupBusinessAsync(businessId.Value);
        }
    }

    private sealed record OutletResponse(Guid Id);
}
