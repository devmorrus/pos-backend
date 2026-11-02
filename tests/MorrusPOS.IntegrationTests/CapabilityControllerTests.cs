using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using MorrusPOS.Application.Common.Interfaces;
using MorrusPOS.Application.Features.Capabilities;
using MorrusPOS.Application.Features.Auth;
using Npgsql;
using System.Text.Json;

namespace MorrusPOS.IntegrationTests;

public class CapabilityControllerTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public CapabilityControllerTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task EffectiveCapabilities_Should_ReturnTenantSnapshot()
    {
        using var scope = _factory.Services.CreateScope();
        var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            jwt.GenerateAccessToken(
                Guid.Parse("a4f78de1-8a9d-4e96-857e-399fa5b5f25a"),
                null,
                "Owner",
                Guid.Parse("11111111-2222-3333-4444-555555555555"),
                "Active",
                DateTime.UtcNow.AddYears(1)));

        var response = await client.GetAsync("/api/capabilities/effective");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var snapshot = await response.Content.ReadFromJsonAsync<EffectiveCapabilitySnapshot>();
        Assert.NotNull(snapshot);
        Assert.Equal(27, snapshot.Capabilities.Count);
        Assert.All(snapshot.Capabilities, capability => Assert.True(capability.Enabled));
    }

    [Fact]
    public async Task DisabledOutletCapability_Should_ReturnStructuredForbidden()
    {
        var adminConnection = Environment.GetEnvironmentVariable("MORRUS_TENANCY_TEST_ADMIN");
        if (string.IsNullOrWhiteSpace(adminConnection)) return;

        var businessId = Guid.NewGuid();
        var outletId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        try
        {
            await using var seed = admin.CreateCommand();
            seed.CommandText = """
                INSERT INTO businesses
                    ("Id","Name","Slug","Category","IndustryProfileId","SubscriptionStatus","TrialStartDate","TrialEndDate","CreatedAt","UpdatedAt")
                VALUES (@business,'Capability Disabled',@slug,'General','ca100001-0000-4000-8000-000000000001','Active',now(),now()+interval '30 days',now(),now());
                INSERT INTO outlets ("Id","BusinessId","Name","Code","IsActive","CreatedAt","UpdatedAt")
                VALUES (@outlet,@business,'Disabled Outlet',@code,true,now(),now());
                INSERT INTO users
                    ("Id","BusinessId","RoleId","Name","Email","PasswordHash","IsActive","CreatedAt","UpdatedAt")
                VALUES (@user,@business,'e1a7b077-44a3-4b63-95e0-59a8501170ea','Owner Disabled',@email,'test',true,now(),now());
                UPDATE businesses SET "OwnerId"=@user WHERE "Id"=@business;
                INSERT INTO outlet_capability_overrides
                    ("BusinessId","OutletId","CapabilityId","IsEnabled","Reason","UpdatedByUserId","CreatedAt","UpdatedAt")
                VALUES (@business,@outlet,'ca200000-0000-4000-8000-000000000009',false,'Test',@user,now(),now());
                """;
            seed.Parameters.AddWithValue("business", businessId);
            seed.Parameters.AddWithValue("outlet", outletId);
            seed.Parameters.AddWithValue("user", userId);
            seed.Parameters.AddWithValue("slug", $"disabled-{suffix}");
            seed.Parameters.AddWithValue("code", $"DIS-{suffix}");
            seed.Parameters.AddWithValue("email", $"disabled-{suffix}@example.test");
            await seed.ExecuteNonQueryAsync();

            using var scope = _factory.Services.CreateScope();
            var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                jwt.GenerateAccessToken(userId, null, "Owner", businessId, "Active", DateTime.UtcNow.AddYears(1)));

            var response = await client.GetAsync($"/api/inventory?outletId={outletId}");
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal("CAPABILITY_DISABLED", payload.RootElement.GetProperty("code").GetString());
            Assert.Equal("inventory.stock", payload.RootElement.GetProperty("capability").GetString());
        }
        finally
        {
            await using var cleanup = admin.CreateCommand();
            cleanup.CommandText = """
                delete from outlet_capability_overrides where "BusinessId"=@business;
                delete from users where "Id"=@user;
                delete from outlets where "Id"=@outlet;
                delete from businesses where "Id"=@business;
                """;
            cleanup.Parameters.AddWithValue("outlet", outletId);
            cleanup.Parameters.AddWithValue("user", userId);
            cleanup.Parameters.AddWithValue("business", businessId);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Owner_Should_UpdateBusinessOverride_WithRevisionAndAudit()
    {
        var adminConnection = Environment.GetEnvironmentVariable("MORRUS_TENANCY_TEST_ADMIN");
        if (string.IsNullOrWhiteSpace(adminConnection)) return;
        var businessId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        try
        {
            await using var seed = admin.CreateCommand();
            seed.CommandText = """
                INSERT INTO businesses
                    ("Id","Name","Slug","Category","IndustryProfileId","SubscriptionStatus","TrialStartDate","TrialEndDate","CreatedAt","UpdatedAt")
                VALUES (@business,'Capability Admin',@slug,'General','ca100001-0000-4000-8000-000000000001','Active',now(),now()+interval '30 days',now(),now());
                INSERT INTO users
                    ("Id","BusinessId","RoleId","Name","Email","PasswordHash","IsActive","CreatedAt","UpdatedAt")
                VALUES (@user,@business,'e1a7b077-44a3-4b63-95e0-59a8501170ea','Owner Test',@email,'test',true,now(),now());
                UPDATE businesses SET "OwnerId"=@user WHERE "Id"=@business;
                """;
            seed.Parameters.AddWithValue("business", businessId);
            seed.Parameters.AddWithValue("user", userId);
            seed.Parameters.AddWithValue("slug", $"cap-admin-{suffix}");
            seed.Parameters.AddWithValue("email", $"cap-{suffix}@example.test");
            await seed.ExecuteNonQueryAsync();

            using var scope = _factory.Services.CreateScope();
            var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                jwt.GenerateAccessToken(userId, null, "Owner", businessId, "Active", DateTime.UtcNow.AddYears(1)));

            var response = await client.PutAsJsonAsync("/api/capabilities/business-overrides", new
            {
                expectedRevision = 0,
                reason = "Integration test",
                items = new[] { new { code = "consignment", enabled = (bool?)true } },
            });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var snapshot = await response.Content.ReadFromJsonAsync<EffectiveCapabilitySnapshot>();
            Assert.NotNull(snapshot);
            Assert.Equal(1, snapshot.Revision);
            Assert.True(snapshot.Capabilities.Single(x => x.Code == "consignment").Enabled);

            var staleResponse = await client.PutAsJsonAsync("/api/capabilities/business-overrides", new
            {
                expectedRevision = 0,
                reason = "Stale update",
                items = new[] { new { code = "consignment", enabled = (bool?)false } },
            });
            Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);

            var history = await client.GetFromJsonAsync<List<CapabilityChangeLogDto>>("/api/capabilities/history");
            Assert.Contains(history!, x => x.CapabilityCode == "consignment" && x.ChangeType == "BusinessOverrideSet");
        }
        finally
        {
            await using var cleanup = admin.CreateCommand();
            cleanup.CommandText = """
                delete from capability_change_logs where "BusinessId"=@business;
                delete from business_capability_overrides where "BusinessId"=@business;
                delete from users where "Id"=@user;
                delete from businesses where "Id"=@business;
                """;
            cleanup.Parameters.AddWithValue("business", businessId);
            cleanup.Parameters.AddWithValue("user", userId);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Registration_Should_AssignSelectedIndustryProfile()
    {
        var adminConnection = Environment.GetEnvironmentVariable("MORRUS_TENANCY_TEST_ADMIN");
        if (string.IsNullOrWhiteSpace(adminConnection)) return;
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register-owner", new RegisterOwnerRequest(
            new RegisterOwnerOwnerDetails("Capability Owner", $"register-{suffix}@example.test", "Password123!"),
            new RegisterOwnerBusinessDetails("Capability Business", "Services", null, "services"),
            "Free Trial",
            new RegisterOwnerOutletDetails("Main Outlet", $"REG-{suffix}", null, null)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(session?.BusinessId);

        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        try
        {
            await using var verify = admin.CreateCommand();
            verify.CommandText = """
                select ip."Code"
                from businesses b join industry_profiles ip on ip."Id"=b."IndustryProfileId"
                where b."Id"=@business;
                """;
            verify.Parameters.AddWithValue("business", session!.BusinessId!.Value);
            Assert.Equal("services", await verify.ExecuteScalarAsync());
        }
        finally
        {
            await using var cleanup = admin.CreateCommand();
            cleanup.CommandText = """
                delete from refresh_tokens where "BusinessId"=@business;
                delete from users where "BusinessId"=@business;
                delete from outlets where "BusinessId"=@business;
                delete from businesses where "Id"=@business;
                """;
            cleanup.Parameters.AddWithValue("business", session!.BusinessId!.Value);
            await cleanup.ExecuteNonQueryAsync();
        }
    }
}
