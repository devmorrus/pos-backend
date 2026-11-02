using Npgsql;

namespace MorrusPOS.IntegrationTests;

public class TenantRlsDatabaseTests
{
    [Fact]
    public async Task Rls_IsolatesTwoBusinesses_AndSystemRoleCanResolveBoth()
    {
        var adminConnection = Environment.GetEnvironmentVariable("MORRUS_TENANCY_TEST_ADMIN");
        var appConnection = Environment.GetEnvironmentVariable("MORRUS_TENANCY_TEST_APP");
        var systemConnection = Environment.GetEnvironmentVariable("MORRUS_TENANCY_TEST_SYSTEM");

        // PostgreSQL is optional for local unit-only runs; CI always supplies it.
        if (string.IsNullOrWhiteSpace(adminConnection) ||
            string.IsNullOrWhiteSpace(appConnection) ||
            string.IsNullOrWhiteSpace(systemConnection))
            return;

        var businessA = Guid.NewGuid();
        var businessB = Guid.NewGuid();
        var outletA = Guid.NewGuid();
        var outletB = Guid.NewGuid();
        var blockedOutlet = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..10];

        await using var admin = new NpgsqlConnection(adminConnection);
        await admin.OpenAsync();

        try
        {
            await using (var seed = admin.CreateCommand())
            {
                seed.CommandText = """
                    INSERT INTO businesses
                        ("Id","Name","Slug","Category","IndustryProfileId","SubscriptionStatus","TrialStartDate","TrialEndDate","CreatedAt","UpdatedAt")
                    VALUES
                        (@business_a,'RLS Tenant A',@slug_a,'Test','ca100001-0000-4000-8000-000000000001','Active',now(),now()+interval '30 days',now(),now()),
                        (@business_b,'RLS Tenant B',@slug_b,'Test','ca100001-0000-4000-8000-000000000001','Active',now(),now()+interval '30 days',now(),now());
                    INSERT INTO outlets
                        ("Id","BusinessId","Name","Code","IsActive","CreatedAt","UpdatedAt")
                    VALUES
                        (@outlet_a,@business_a,'RLS Outlet A',@code_a,true,now(),now()),
                        (@outlet_b,@business_b,'RLS Outlet B',@code_b,true,now(),now());
                    """;
                seed.Parameters.AddWithValue("business_a", businessA);
                seed.Parameters.AddWithValue("business_b", businessB);
                seed.Parameters.AddWithValue("outlet_a", outletA);
                seed.Parameters.AddWithValue("outlet_b", outletB);
                seed.Parameters.AddWithValue("slug_a", $"rls-a-{suffix}");
                seed.Parameters.AddWithValue("slug_b", $"rls-b-{suffix}");
                seed.Parameters.AddWithValue("code_a", $"RLS-A-{suffix}");
                seed.Parameters.AddWithValue("code_b", $"RLS-B-{suffix}");
                await seed.ExecuteNonQueryAsync();
            }

            await using var app = new NpgsqlConnection(appConnection);
            await app.OpenAsync();

            await SetBusinessAsync(app, null);
            Assert.Equal(0, await CountTestOutletsAsync(app, outletA, outletB));

            await SetBusinessAsync(app, businessA);
            Assert.Equal(1, await CountTestOutletsAsync(app, outletA, outletB));
            Assert.Equal(businessA, await ReadVisibleBusinessAsync(app));
            await using (var updateOwnBusiness = app.CreateCommand())
            {
                updateOwnBusiness.CommandText = "update businesses set \"UpdatedAt\"=now() where \"Id\" in (@a,@b);";
                updateOwnBusiness.Parameters.AddWithValue("a", businessA);
                updateOwnBusiness.Parameters.AddWithValue("b", businessB);
                Assert.Equal(1, await updateOwnBusiness.ExecuteNonQueryAsync());
            }

            await SetBusinessAsync(app, businessB);
            Assert.Equal(1, await CountTestOutletsAsync(app, outletA, outletB));
            Assert.Equal(businessB, await ReadVisibleBusinessAsync(app));

            // Active context B cannot create a row owned by tenant A.
            await using var blocked = app.CreateCommand();
            blocked.CommandText = """
                INSERT INTO outlets
                    ("Id","BusinessId","Name","Code","IsActive","CreatedAt","UpdatedAt")
                VALUES (@id,@business,'Blocked Cross Tenant',@code,true,now(),now());
                """;
            blocked.Parameters.AddWithValue("id", blockedOutlet);
            blocked.Parameters.AddWithValue("business", businessA);
            blocked.Parameters.AddWithValue("code", $"RLS-X-{suffix}");
            var exception = await Assert.ThrowsAsync<PostgresException>(() => blocked.ExecuteNonQueryAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
            await AssertPermissionDeniedAsync(app, "select count(*) from refresh_tokens;");

            await using var system = new NpgsqlConnection(systemConnection);
            await system.OpenAsync();
            Assert.Equal(2, await CountTestOutletsAsync(system, outletA, outletB));
            await AssertPermissionDeniedAsync(system, "select count(*) from products;");
        }
        finally
        {
            await using var cleanup = admin.CreateCommand();
            cleanup.CommandText = """
                DELETE FROM outlets WHERE "Id" IN (@outlet_a,@outlet_b,@blocked);
                DELETE FROM businesses WHERE "Id" IN (@business_a,@business_b);
                """;
            cleanup.Parameters.AddWithValue("outlet_a", outletA);
            cleanup.Parameters.AddWithValue("outlet_b", outletB);
            cleanup.Parameters.AddWithValue("blocked", blockedOutlet);
            cleanup.Parameters.AddWithValue("business_a", businessA);
            cleanup.Parameters.AddWithValue("business_b", businessB);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    private static async Task SetBusinessAsync(NpgsqlConnection connection, Guid? businessId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select set_config('app.business_id',@business_id,false);";
        command.Parameters.AddWithValue("business_id", businessId?.ToString() ?? string.Empty);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> CountTestOutletsAsync(
        NpgsqlConnection connection,
        Guid outletA,
        Guid outletB)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*)::int from outlets where \"Id\" in (@outlet_a,@outlet_b);";
        command.Parameters.AddWithValue("outlet_a", outletA);
        command.Parameters.AddWithValue("outlet_b", outletB);
        return (int)(await command.ExecuteScalarAsync())!;
    }

    private static async Task<Guid> ReadVisibleBusinessAsync(NpgsqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select \"Id\" from businesses;";
        return (Guid)(await command.ExecuteScalarAsync())!;
    }

    private static async Task AssertPermissionDeniedAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var exception = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteScalarAsync());
        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }
}
