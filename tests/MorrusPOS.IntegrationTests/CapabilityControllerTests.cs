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
        Assert.Equal(28, snapshot.Capabilities.Count);
        Assert.All(snapshot.Capabilities.Where(c => c.Code != "catalog.serial_tracking"),
            capability => Assert.True(capability.Enabled));
        Assert.Contains(snapshot.Capabilities,
            capability => capability.Code == "catalog.serial_tracking" && !capability.Enabled);
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

    [Theory]
    [InlineData("Elektronik", "electronics", "Elektronik", "inventory.transfer", "catalog.modifiers")]
    [InlineData("F&B", "food-beverage", "F&B", "catalog.modifiers", "inventory.transfer")]
    [InlineData("Retail Umum", "retail", "Retail Umum", "procurement.supplier_debt", "channels.gobiz")]
    public async Task Registration_Should_AssignReadyIndustryProfile(
        string category,
        string profileCode,
        string profileName,
        string enabledCapability,
        string disabledCapability)
    {
        var adminConnection = Environment.GetEnvironmentVariable("MORRUS_TENANCY_TEST_ADMIN");
        if (string.IsNullOrWhiteSpace(adminConnection)) return;
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register-owner", new RegisterOwnerRequest(
            new RegisterOwnerOwnerDetails("Capability Owner", $"register-{suffix}@example.test", "Password123!"),
            new RegisterOwnerBusinessDetails("Capability Business", category, null, profileCode),
            "Free Trial",
            new RegisterOwnerOutletDetails("Main Outlet", $"REG-{suffix}", null, null)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(session?.BusinessId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        var snapshot = await client.GetFromJsonAsync<EffectiveCapabilitySnapshot>("/api/capabilities/effective");
        Assert.NotNull(snapshot);
        Assert.Equal(profileCode, snapshot.IndustryProfile.Code);
        Assert.True(snapshot.Capabilities.Single(x => x.Code == enabledCapability).Enabled);
        Assert.False(snapshot.Capabilities.Single(x => x.Code == disabledCapability).Enabled);

        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        try
        {
            await using var verify = admin.CreateCommand();
            verify.CommandText = """
                select ip."Code", ip."Name"
                from businesses b join industry_profiles ip on ip."Id"=b."IndustryProfileId"
                where b."Id"=@business;
                """;
            verify.Parameters.AddWithValue("business", session!.BusinessId!.Value);
            await using var reader = await verify.ExecuteReaderAsync();
            Assert.True(await reader.ReadAsync());
            Assert.Equal(profileCode, reader.GetString(0));
            Assert.Equal(profileName, reader.GetString(1));
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

    [Fact]
    public async Task ActiveIndustryProfiles_Should_MapEveryActiveCapability()
    {
        var adminConnection = Environment.GetEnvironmentVariable("MORRUS_TENANCY_TEST_ADMIN");
        if (string.IsNullOrWhiteSpace(adminConnection)) return;

        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var command = admin.CreateCommand();
        command.CommandText = """
            select count(*)
            from industry_profiles profile
            where profile."IsActive"
              and (select count(*)
                   from industry_profile_capabilities mapping
                   join capabilities capability on capability."Id"=mapping."CapabilityId"
                   where mapping."IndustryProfileId"=profile."Id" and capability."IsActive")
                  <> (select count(*) from capabilities where "IsActive");
            """;

        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync())!);
    }

    [Fact]
    public async Task MixedControllers_Should_EvaluateEachActionCapabilityIndependently()
    {
        var adminConnection = Environment.GetEnvironmentVariable("MORRUS_TENANCY_TEST_ADMIN");
        if (string.IsNullOrWhiteSpace(adminConnection)) return;

        var businessId = Guid.NewGuid();
        var outletId = Guid.NewGuid();
        var disabledOutletId = Guid.NewGuid();
        var cashierSessionId = Guid.NewGuid();
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
                VALUES (@business,'Mixed Capabilities',@slug,'General','ca100001-0000-4000-8000-000000000001','Active',now(),now()+interval '30 days',now(),now());
                INSERT INTO outlets ("Id","BusinessId","Name","Code","IsActive","CreatedAt","UpdatedAt")
                VALUES
                    (@outlet,@business,'Mixed Outlet',@code,true,now(),now()),
                    (@disabledOutlet,@business,'Disabled POS Outlet',@disabledCode,true,now(),now());
                INSERT INTO users
                    ("Id","BusinessId","RoleId","Name","Email","PasswordHash","IsActive","CreatedAt","UpdatedAt")
                VALUES (@user,@business,'e1a7b077-44a3-4b63-95e0-59a8501170ea','Mixed Owner',@email,'test',true,now(),now());
                UPDATE businesses SET "OwnerId"=@user WHERE "Id"=@business;
                INSERT INTO cashier_sessions
                    ("Id","BusinessId","OutletId","UserId","OpeningTime","OpeningCash","ExpectedCash","Status","CreatedAt","UpdatedAt")
                VALUES (@cashierSession,@business,@disabledOutlet,@user,now(),0,0,'open',now(),now());
                INSERT INTO business_capability_overrides
                    ("BusinessId","CapabilityId","IsEnabled","Reason","UpdatedByUserId","CreatedAt","UpdatedAt")
                VALUES
                    (@business,'ca200000-0000-4000-8000-000000000002',false,'Separate POS from history',@user,now(),now()),
                    (@business,'ca200000-0000-4000-8000-000000000025',false,'Separate report groups',@user,now(),now()),
                    (@business,'ca200000-0000-4000-8000-000000000026',true,'Separate report groups',@user,now(),now());
                INSERT INTO outlet_capability_overrides
                    ("BusinessId","OutletId","CapabilityId","IsEnabled","Reason","UpdatedByUserId","CreatedAt","UpdatedAt")
                VALUES (@business,@disabledOutlet,'ca200000-0000-4000-8000-000000000001',false,'Disable POS',@user,now(),now());
                """;
            seed.Parameters.AddWithValue("business", businessId);
            seed.Parameters.AddWithValue("outlet", outletId);
            seed.Parameters.AddWithValue("disabledOutlet", disabledOutletId);
            seed.Parameters.AddWithValue("cashierSession", cashierSessionId);
            seed.Parameters.AddWithValue("user", userId);
            seed.Parameters.AddWithValue("slug", $"mixed-{suffix}");
            seed.Parameters.AddWithValue("code", $"MIX-{suffix}");
            seed.Parameters.AddWithValue("disabledCode", $"OFF-{suffix}");
            seed.Parameters.AddWithValue("email", $"mixed-{suffix}@example.test");
            await seed.ExecuteNonQueryAsync();

            using var scope = _factory.Services.CreateScope();
            var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                jwt.GenerateAccessToken(userId, null, "Owner", businessId, "Active", DateTime.UtcNow.AddYears(1)));

            var checkout = await client.PostAsJsonAsync("/api/transactions/checkout", new { });
            Assert.NotEqual(HttpStatusCode.Forbidden, checkout.StatusCode);

            var history = await client.GetAsync($"/api/transactions?outletId={outletId}");
            Assert.Equal(HttpStatusCode.Forbidden, history.StatusCode);
            using (var payload = JsonDocument.Parse(await history.Content.ReadAsStringAsync()))
                Assert.Equal("sales.transactions", payload.RootElement.GetProperty("capability").GetString());

            var accountingReport = await client.GetAsync(
                "/api/reports/profit-loss?startDate=2026-01-01&endDate=2026-01-31");
            Assert.NotEqual(HttpStatusCode.Forbidden, accountingReport.StatusCode);

            var operationalReport = await client.GetAsync(
                "/api/reports/sales?startDate=2026-01-01&endDate=2026-01-31");
            Assert.Equal(HttpStatusCode.Forbidden, operationalReport.StatusCode);
            using (var payload = JsonDocument.Parse(await operationalReport.Content.ReadAsStringAsync()))
                Assert.Equal("reports.operational", payload.RootElement.GetProperty("capability").GetString());

            var goBizStatus = await client.GetAsync($"/api/gobiz/status?outletId={outletId}");
            Assert.Equal(HttpStatusCode.Forbidden, goBizStatus.StatusCode);
            using (var payload = JsonDocument.Parse(await goBizStatus.Content.ReadAsStringAsync()))
                Assert.Equal("channels.gobiz", payload.RootElement.GetProperty("capability").GetString());

            var closeDisabledSession = await client.PostAsJsonAsync(
                $"/api/cashier-sessions/close/{cashierSessionId}", new { actualCash = 0 });
            Assert.Equal(HttpStatusCode.Forbidden, closeDisabledSession.StatusCode);
            using (var payload = JsonDocument.Parse(await closeDisabledSession.Content.ReadAsStringAsync()))
                Assert.Equal("sales.pos", payload.RootElement.GetProperty("capability").GetString());
        }
        finally
        {
            await using var cleanup = admin.CreateCommand();
            cleanup.CommandText = """
                delete from business_capability_overrides where "BusinessId"=@business;
                delete from outlet_capability_overrides where "BusinessId"=@business;
                delete from cashier_sessions where "Id"=@cashierSession;
                delete from users where "Id"=@user;
                delete from outlets where "Id" in (@outlet,@disabledOutlet);
                delete from businesses where "Id"=@business;
                """;
            cleanup.Parameters.AddWithValue("business", businessId);
            cleanup.Parameters.AddWithValue("outlet", outletId);
            cleanup.Parameters.AddWithValue("disabledOutlet", disabledOutletId);
            cleanup.Parameters.AddWithValue("cashierSession", cashierSessionId);
            cleanup.Parameters.AddWithValue("user", userId);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Admin_Should_ReadCapabilityConfiguration_ButNotChangeIt()
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
                VALUES (@business,'Capability Read Only',@slug,'General','ca100001-0000-4000-8000-000000000001','Active',now(),now()+interval '30 days',now(),now());
                INSERT INTO users
                    ("Id","BusinessId","RoleId","Name","Email","PasswordHash","IsActive","CreatedAt","UpdatedAt")
                VALUES (@user,@business,'d54f590a-6e54-4f05-8461-8ff62dfd8d4c','Admin Test',@email,'test',true,now(),now());
                """;
            seed.Parameters.AddWithValue("business", businessId);
            seed.Parameters.AddWithValue("user", userId);
            seed.Parameters.AddWithValue("slug", $"cap-readonly-{suffix}");
            seed.Parameters.AddWithValue("email", $"cap-readonly-{suffix}@example.test");
            await seed.ExecuteNonQueryAsync();

            using var scope = _factory.Services.CreateScope();
            var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                jwt.GenerateAccessToken(userId, null, "Admin", businessId, "Active", DateTime.UtcNow.AddYears(1)));

            var configuration = await client.GetAsync("/api/capabilities/configuration");
            Assert.Equal(HttpStatusCode.OK, configuration.StatusCode);

            var profileChange = await client.PutAsJsonAsync("/api/capabilities/industry-profile", new
            {
                profileCode = "electronics",
                expectedRevision = 0,
                reason = "Admin tidak boleh mengubah",
            });
            Assert.Equal(HttpStatusCode.Forbidden, profileChange.StatusCode);

            var overrideChange = await client.PutAsJsonAsync("/api/capabilities/business-overrides", new
            {
                expectedRevision = 0,
                reason = "Admin tidak boleh mengubah",
                items = new[] { new { code = "consignment", enabled = (bool?)true } },
            });
            Assert.Equal(HttpStatusCode.Forbidden, overrideChange.StatusCode);
        }
        finally
        {
            await using var cleanup = admin.CreateCommand();
            cleanup.CommandText = """
                delete from users where "Id"=@user;
                delete from businesses where "Id"=@business;
                """;
            cleanup.Parameters.AddWithValue("business", businessId);
            cleanup.Parameters.AddWithValue("user", userId);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Tenant_Should_NotReadAnotherTenantConfiguration()
    {
        var adminConnection = Environment.GetEnvironmentVariable("MORRUS_TENANCY_TEST_ADMIN");
        if (string.IsNullOrWhiteSpace(adminConnection)) return;

        var businessA = Guid.NewGuid();
        var businessB = Guid.NewGuid();
        var outletA = Guid.NewGuid();
        var outletB = Guid.NewGuid();
        var userA = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        try
        {
            await using var seed = admin.CreateCommand();
            seed.CommandText = """
                INSERT INTO businesses
                    ("Id","Name","Slug","Category","IndustryProfileId","SubscriptionStatus","TrialStartDate","TrialEndDate","CreatedAt","UpdatedAt")
                VALUES
                    (@businessA,'Tenant A',@slugA,'General','ca100001-0000-4000-8000-000000000001','Active',now(),now()+interval '30 days',now(),now()),
                    (@businessB,'Tenant B',@slugB,'General','ca100001-0000-4000-8000-000000000001','Active',now(),now()+interval '30 days',now(),now());
                INSERT INTO outlets ("Id","BusinessId","Name","Code","IsActive","CreatedAt","UpdatedAt")
                VALUES
                    (@outletA,@businessA,'Outlet A',@codeA,true,now(),now()),
                    (@outletB,@businessB,'Outlet B',@codeB,true,now(),now());
                INSERT INTO users
                    ("Id","BusinessId","RoleId","Name","Email","PasswordHash","IsActive","CreatedAt","UpdatedAt")
                VALUES (@userA,@businessA,'e1a7b077-44a3-4b63-95e0-59a8501170ea','Owner A',@emailA,'test',true,now(),now());
                """;
            seed.Parameters.AddWithValue("businessA", businessA);
            seed.Parameters.AddWithValue("businessB", businessB);
            seed.Parameters.AddWithValue("outletA", outletA);
            seed.Parameters.AddWithValue("outletB", outletB);
            seed.Parameters.AddWithValue("userA", userA);
            seed.Parameters.AddWithValue("slugA", $"tenant-a-{suffix}");
            seed.Parameters.AddWithValue("slugB", $"tenant-b-{suffix}");
            seed.Parameters.AddWithValue("codeA", $"TA-{suffix}");
            seed.Parameters.AddWithValue("codeB", $"TB-{suffix}");
            seed.Parameters.AddWithValue("emailA", $"tenant-a-{suffix}@example.test");
            await seed.ExecuteNonQueryAsync();

            using var scope = _factory.Services.CreateScope();
            var jwt = scope.ServiceProvider.GetRequiredService<IJwtTokenService>();
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                jwt.GenerateAccessToken(userA, null, "Owner", businessA, "Active", DateTime.UtcNow.AddYears(1)));

            var effective = await client.GetAsync($"/api/capabilities/effective?outletId={outletB}");
            Assert.Equal(HttpStatusCode.NotFound, effective.StatusCode);

            var configuration = await client.GetAsync($"/api/capabilities/configuration?outletId={outletB}");
            Assert.Equal(HttpStatusCode.NotFound, configuration.StatusCode);
        }
        finally
        {
            await using var cleanup = admin.CreateCommand();
            cleanup.CommandText = """
                delete from users where "Id"=@userA;
                delete from outlets where "Id" in (@outletA,@outletB);
                delete from businesses where "Id" in (@businessA,@businessB);
                """;
            cleanup.Parameters.AddWithValue("userA", userA);
            cleanup.Parameters.AddWithValue("outletA", outletA);
            cleanup.Parameters.AddWithValue("outletB", outletB);
            cleanup.Parameters.AddWithValue("businessA", businessA);
            cleanup.Parameters.AddWithValue("businessB", businessB);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    [Fact]
    public async Task Owner_Should_ChangeIndustryProfile_WithRevisionAndAudit()
    {
        var adminConnection = Environment.GetEnvironmentVariable("MORRUS_TENANCY_TEST_ADMIN");
        if (string.IsNullOrWhiteSpace(adminConnection)) return;

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var client = _factory.CreateClient();
        var registration = await client.PostAsJsonAsync("/api/auth/register-owner", new RegisterOwnerRequest(
            new RegisterOwnerOwnerDetails("Profile Owner", $"profile-{suffix}@example.test", "Password123!"),
            new RegisterOwnerBusinessDetails("Profile Business", "Retail Umum", null, "retail"),
            "Free Trial",
            new RegisterOwnerOutletDetails("Main Outlet", $"PRO-{suffix}", null, null)));
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        var session = await registration.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(session?.BusinessId);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        var response = await client.PutAsJsonAsync("/api/capabilities/industry-profile", new
        {
            profileCode = "electronics",
            expectedRevision = 0,
            reason = "Menyesuaikan jenis usaha",
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var snapshot = await response.Content.ReadFromJsonAsync<EffectiveCapabilitySnapshot>();
        Assert.NotNull(snapshot);
        Assert.Equal(1, snapshot.Revision);
        Assert.Equal("electronics", snapshot.IndustryProfile.Code);

        var history = await client.GetFromJsonAsync<List<CapabilityChangeLogDto>>("/api/capabilities/history");
        Assert.Contains(history!, x =>
            x.ChangeType == "IndustryProfileChanged" && x.Reason == "Menyesuaikan jenis usaha");

        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();
        await using var cleanup = admin.CreateCommand();
        cleanup.CommandText = """
            delete from capability_change_logs where "BusinessId"=@business;
            delete from refresh_tokens where "BusinessId"=@business;
            delete from users where "BusinessId"=@business;
            delete from outlets where "BusinessId"=@business;
            delete from businesses where "Id"=@business;
            """;
        cleanup.Parameters.AddWithValue("business", session.BusinessId.Value);
        await cleanup.ExecuteNonQueryAsync();
    }
}
