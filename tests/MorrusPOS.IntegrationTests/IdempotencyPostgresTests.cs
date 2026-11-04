using Npgsql;

namespace MorrusPOS.IntegrationTests;

public class IdempotencyPostgresTests
{
    [Fact]
    public async Task Receipt_IsTenantScopedUniqueImmutable_AndAdvisoryLockSerializes()
    {
        var adminConnection = Environment.GetEnvironmentVariable("MORRUS_TENANCY_TEST_ADMIN");
        var appConnection = Environment.GetEnvironmentVariable("MORRUS_TENANCY_TEST_APP");
        if (string.IsNullOrWhiteSpace(adminConnection) || string.IsNullOrWhiteSpace(appConnection)) return;

        var businessId = Guid.NewGuid();
        var outletId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var key = Guid.NewGuid();
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
                    VALUES (@business,'Idempotency Test',@slug,'Test','ca100001-0000-4000-8000-000000000001','Active',now(),now()+interval '1 day',now(),now());
                    INSERT INTO outlets ("Id","BusinessId","Name","Code","IsActive","CreatedAt","UpdatedAt")
                    VALUES (@outlet,@business,'Idempotency Outlet',@code,true,now(),now());
                    INSERT INTO users
                        ("Id","BusinessId","OutletId","RoleId","Name","Email","PasswordHash","IsActive","CreatedAt","UpdatedAt")
                    SELECT @user,@business,@outlet,"Id",'Idempotency User',@email,'hash',true,now(),now()
                    FROM roles WHERE "Name"='Admin' LIMIT 1;
                    """;
                seed.Parameters.AddWithValue("business", businessId);
                seed.Parameters.AddWithValue("outlet", outletId);
                seed.Parameters.AddWithValue("user", userId);
                seed.Parameters.AddWithValue("slug", $"idem-{suffix}");
                seed.Parameters.AddWithValue("code", $"IDM-{suffix}");
                seed.Parameters.AddWithValue("email", $"idem-{suffix}@test.local");
                await seed.ExecuteNonQueryAsync();
            }

            await using var app = new NpgsqlConnection(appConnection);
            await app.OpenAsync();
            await SetBusinessAsync(app, businessId);
            await using (var insert = app.CreateCommand())
            {
                insert.CommandText = """
                    INSERT INTO idempotency_records
                        ("Id","BusinessId","IdempotencyKey","Operation","RequestHash","ResourceType","ResourceId","UserId","OutletId","CreatedAt")
                    VALUES (gen_random_uuid(),@business,@key,'checkout',repeat('a',64),'transaction',gen_random_uuid(),@user,@outlet,now());
                    """;
                insert.Parameters.AddWithValue("business", businessId);
                insert.Parameters.AddWithValue("key", key);
                insert.Parameters.AddWithValue("user", userId);
                insert.Parameters.AddWithValue("outlet", outletId);
                await insert.ExecuteNonQueryAsync();
                var duplicate = await Assert.ThrowsAsync<PostgresException>(() => insert.ExecuteNonQueryAsync());
                Assert.Equal(PostgresErrorCodes.UniqueViolation, duplicate.SqlState);
            }
            await using (var immutable = app.CreateCommand())
            {
                immutable.CommandText = "DELETE FROM idempotency_records WHERE \"IdempotencyKey\"=@key;";
                immutable.Parameters.AddWithValue("key", key);
                var exception = await Assert.ThrowsAsync<PostgresException>(() => immutable.ExecuteNonQueryAsync());
                Assert.Contains(exception.SqlState, new[] { PostgresErrorCodes.InsufficientPrivilege, PostgresErrorCodes.RaiseException });
            }

            await using var app2 = new NpgsqlConnection(appConnection);
            await app2.OpenAsync();
            await SetBusinessAsync(app2, businessId);
            await using var tx1 = await app.BeginTransactionAsync();
            await using (var lock1 = app.CreateCommand())
            {
                lock1.Transaction = tx1;
                lock1.CommandText = "SELECT pg_advisory_xact_lock(hashtextextended(@key,0));";
                lock1.Parameters.AddWithValue("key", $"{businessId:N}:checkout:{key:N}");
                await lock1.ExecuteNonQueryAsync();
            }
            await using var tx2 = await app2.BeginTransactionAsync();
            await using (var lock2 = app2.CreateCommand())
            {
                lock2.Transaction = tx2;
                lock2.CommandText = "SELECT pg_try_advisory_xact_lock(hashtextextended(@key,0));";
                lock2.Parameters.AddWithValue("key", $"{businessId:N}:checkout:{key:N}");
                Assert.False((bool)(await lock2.ExecuteScalarAsync())!);
            }
            await tx2.RollbackAsync();
            await tx1.RollbackAsync();
        }
        finally
        {
            await using var cleanup = admin.CreateCommand();
            cleanup.CommandText = """
                BEGIN;
                LOCK TABLE idempotency_records IN ACCESS EXCLUSIVE MODE;
                ALTER TABLE idempotency_records DISABLE TRIGGER trg_prevent_idempotency_mutation;
                DELETE FROM idempotency_records WHERE "BusinessId"=@business;
                ALTER TABLE idempotency_records ENABLE TRIGGER trg_prevent_idempotency_mutation;
                DELETE FROM users WHERE "BusinessId"=@business;
                DELETE FROM outlets WHERE "BusinessId"=@business;
                DELETE FROM businesses WHERE "Id"=@business;
                COMMIT;
                """;
            cleanup.Parameters.AddWithValue("business", businessId);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    private static async Task SetBusinessAsync(NpgsqlConnection connection, Guid businessId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select set_config('app.business_id',@business,false);";
        command.Parameters.AddWithValue("business", businessId.ToString());
        await command.ExecuteNonQueryAsync();
    }
}
