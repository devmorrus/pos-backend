using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MorrusPOS.Application.Common.Events;
using MorrusPOS.Infrastructure.Outbox;
using MorrusPOS.Infrastructure.Persistence;
using Npgsql;

namespace MorrusPOS.IntegrationTests;

public sealed class OutboxPostgresTests
{
    [Fact]
    public async Task Writer_CommitsAtomically_Deduplicates_AndRuntimeCannotReadOrMutate()
    {
        var fixture = await OutboxFixture.CreateAsync();
        try
        {
            var eventId = Guid.NewGuid();
            await WriteAsync(fixture, eventId, "dedupe-key", commit: false);
            Assert.Equal(0, await fixture.CountAsync());

            await WriteAsync(fixture, eventId, "dedupe-key", commit: true);
            await WriteAsync(fixture, Guid.NewGuid(), "dedupe-key", commit: true);
            Assert.Equal(1, await fixture.CountAsync());

            await using var app = new NpgsqlConnection(fixture.AppConnection);
            await app.OpenAsync();
            await using var denied = app.CreateCommand();
            denied.CommandText = "select \"PayloadJson\" from outbox_messages limit 1;";
            var deniedException = await Assert.ThrowsAsync<PostgresException>(() => denied.ExecuteScalarAsync());
            Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, deniedException.SqlState);

            await using var system = new NpgsqlConnection(fixture.SystemConnection);
            await system.OpenAsync();
            await using var immutable = system.CreateCommand();
            immutable.CommandText = "update outbox_messages set \"EventType\"='changed' where \"Id\"=@id;";
            immutable.Parameters.AddWithValue("id", eventId);
            var immutableException = await Assert.ThrowsAsync<PostgresException>(() => immutable.ExecuteNonQueryAsync());
            Assert.Contains("OUTBOX_IMMUTABLE", immutableException.MessageText);
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    [Fact]
    public async Task Store_ClaimsOnce_Retries_Processes_AndRecoversExpiredLease()
    {
        var fixture = await OutboxFixture.CreateAsync();
        try
        {
            await WriteAsync(fixture, Guid.NewGuid(), "first", commit: true);
            await WriteAsync(fixture, Guid.NewGuid(), "second", commit: true);

            await using var firstContext = fixture.CreateSystemContext();
            await using var secondContext = fixture.CreateSystemContext();
            var firstStore = new OutboxStore(firstContext, fixture.Options);
            var secondStore = new OutboxStore(secondContext, fixture.Options);
            var claims = await Task.WhenAll(
                firstStore.ClaimBatchAsync("worker-a"),
                secondStore.ClaimBatchAsync("worker-b"));
            var claimed = claims.SelectMany(x => x).ToArray();
            Assert.Equal(2, claimed.Length);
            Assert.Equal(2, claimed.Select(x => x.Id).Distinct().Count());

            var retry = claimed[0];
            Assert.True(await firstStore.MarkFailedAsync(
                retry.Id, retry.LockId, "TRANSIENT", "temporary", DateTime.UtcNow.AddMilliseconds(-1)));
            var success = claimed[1];
            Assert.True(await secondStore.MarkProcessedAsync(success.Id, success.LockId));

            var retried = Assert.Single(await firstStore.ClaimBatchAsync("worker-a"));
            Assert.Equal(retry.Id, retried.Id);
            Assert.Equal(2, retried.AttemptCount);

            await fixture.ExpireLeaseAsync(retried.Id);
            Assert.Equal(1, await firstStore.RecoverExpiredLeasesAsync());
            var reclaimed = Assert.Single(await firstStore.ClaimBatchAsync("worker-c"));
            Assert.Equal(retry.Id, reclaimed.Id);
            Assert.True(await firstStore.MarkProcessedAsync(reclaimed.Id, reclaimed.LockId));

            var poisonId = Guid.NewGuid();
            await WriteAsync(fixture, poisonId, "poison", commit: true);
            var poison = Assert.Single(await firstStore.ClaimBatchAsync("worker-a"));
            Assert.True(await firstStore.MarkFailedAsync(
                poison.Id, poison.LockId, "OUTBOX_INVALID_PAYLOAD", "invalid", retryAtUtc: null));
            Assert.Equal("dead_letter", await fixture.ReadStatusAsync(poisonId));

            await fixture.RequeueAsync(poisonId);
            Assert.Equal("pending", await fixture.ReadStatusAsync(poisonId));
            var replayed = Assert.Single(await firstStore.ClaimBatchAsync("worker-replay"));
            Assert.Equal(poisonId, replayed.Id);
            Assert.Equal(1, replayed.AttemptCount);

            await fixture.MakeProcessedOldAsync(success.Id);
            Assert.Equal(1, await firstStore.DeleteProcessedBeforeAsync(DateTime.UtcNow.AddDays(-14), 1000));
        }
        finally
        {
            await fixture.DisposeAsync();
        }
    }

    private static async Task WriteAsync(OutboxFixture fixture, Guid eventId, string dedupeKey, bool commit)
    {
        await using var context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(fixture.AppConnection).Options);
        await context.Database.OpenConnectionAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"select set_config('app.business_id',{fixture.BusinessId.ToString()},true);");
        var writer = new OutboxWriter(context, fixture.Options);
        await writer.EnqueueAsync(new OutboxWriteRequest(
            eventId,
            fixture.BusinessId,
            fixture.OutletId,
            OutboxEventTypes.StockUpdated,
            1,
            "{\"outletId\":\"00000000-0000-0000-0000-000000000000\",\"items\":[]}",
            dedupeKey,
            null,
            DateTime.UtcNow));
        if (commit) await transaction.CommitAsync();
        else await transaction.RollbackAsync();
    }

    private sealed class OutboxFixture : IAsyncDisposable
    {
        public Guid BusinessId { get; } = Guid.NewGuid();
        public Guid OutletId { get; } = Guid.NewGuid();
        public string AdminConnection { get; } = IntegrationTestDatabase.RequireConnectionString("MORRUS_TENANCY_TEST_ADMIN");
        public string AppConnection { get; } = IntegrationTestDatabase.RequireConnectionString("MORRUS_TENANCY_TEST_APP");
        public string SystemConnection { get; } = IntegrationTestDatabase.RequireConnectionString("MORRUS_TENANCY_TEST_SYSTEM");
        public IOptions<OutboxOptions> Options { get; } = Microsoft.Extensions.Options.Options.Create(new OutboxOptions
        {
            BatchSize = 1,
            LeaseSeconds = 1,
            MaxAttempts = 8,
        });

        public static async Task<OutboxFixture> CreateAsync()
        {
            var fixture = new OutboxFixture();
            await using var admin = new NpgsqlConnection(fixture.AdminConnection);
            await admin.OpenAsync();
            await using var command = admin.CreateCommand();
            command.CommandText = """
                INSERT INTO businesses
                    ("Id","Name","Slug","Category","IndustryProfileId","SubscriptionStatus","TrialStartDate","TrialEndDate","CreatedAt","UpdatedAt")
                VALUES (@business,'Outbox Test',@slug,'Test','ca100001-0000-4000-8000-000000000001','Active',now(),now()+interval '1 day',now(),now());
                INSERT INTO outlets ("Id","BusinessId","Name","Code","IsActive","CreatedAt","UpdatedAt")
                VALUES (@outlet,@business,'Outbox Test Outlet',@code,true,now(),now());
                """;
            command.Parameters.AddWithValue("business", fixture.BusinessId);
            command.Parameters.AddWithValue("outlet", fixture.OutletId);
            command.Parameters.AddWithValue("slug", $"outbox-{fixture.BusinessId:N}");
            command.Parameters.AddWithValue("code", $"OB-{fixture.BusinessId:N}"[..20]);
            await command.ExecuteNonQueryAsync();
            return fixture;
        }

        public SystemDbContext CreateSystemContext()
            => new(new DbContextOptionsBuilder<SystemDbContext>().UseNpgsql(SystemConnection).Options);

        public async Task<int> CountAsync()
        {
            await using var connection = new NpgsqlConnection(AdminConnection);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "select count(*)::int from outbox_messages where \"BusinessId\"=@business;";
            command.Parameters.AddWithValue("business", BusinessId);
            return (int)(await command.ExecuteScalarAsync())!;
        }

        public async Task ExpireLeaseAsync(Guid id)
        {
            await using var connection = new NpgsqlConnection(AdminConnection);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "update outbox_messages set \"LockedAtUtc\"=now()-interval '5 minutes' where \"Id\"=@id;";
            command.Parameters.AddWithValue("id", id);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<string> ReadStatusAsync(Guid id)
        {
            await using var connection = new NpgsqlConnection(AdminConnection);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "select \"Status\" from outbox_messages where \"Id\"=@id;";
            command.Parameters.AddWithValue("id", id);
            return (string)(await command.ExecuteScalarAsync())!;
        }

        public async Task RequeueAsync(Guid id)
        {
            await using var connection = new NpgsqlConnection(AdminConnection);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                update outbox_messages
                set "Status"='pending',"AttemptCount"=0,"AvailableAtUtc"=now(),
                    "DeadLetteredAtUtc"=null,"ManualReplayCount"="ManualReplayCount"+1,
                    "LastReplayedAtUtc"=now(),"LastReplayedBy"='integration-test',
                    "LastReplayReason"='verified fix'
                where "Id"=@id and "Status"='dead_letter';
                """;
            command.Parameters.AddWithValue("id", id);
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }

        public async Task MakeProcessedOldAsync(Guid id)
        {
            await using var connection = new NpgsqlConnection(AdminConnection);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "update outbox_messages set \"ProcessedAtUtc\"=now()-interval '30 days' where \"Id\"=@id;";
            command.Parameters.AddWithValue("id", id);
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }

        public async ValueTask DisposeAsync()
            => await IntegrationTestDatabase.CleanupBusinessAsync(BusinessId);
    }
}
