using Npgsql;

namespace MorrusPOS.IntegrationTests;

public class BalancedJournalPostgresTests
{
    [Fact]
    public async Task Database_RejectsUnbalancedJournal_AndProtectsPostedJournal()
    {
        var adminCs = Environment.GetEnvironmentVariable("MORRUS_TENANCY_TEST_ADMIN");
        var appCs = Environment.GetEnvironmentVariable("MORRUS_TENANCY_TEST_APP");
        if (string.IsNullOrWhiteSpace(adminCs) || string.IsNullOrWhiteSpace(appCs)) return;
        var business = Guid.NewGuid();
        var outlet = Guid.NewGuid();
        var debitAccount = Guid.NewGuid();
        var creditAccount = Guid.NewGuid();
        var suffix = Guid.NewGuid().ToString("N")[..10];
        await using var admin = new NpgsqlConnection(adminCs);
        await admin.OpenAsync();
        try
        {
            await using (var seed = admin.CreateCommand())
            {
                seed.CommandText = """
                    INSERT INTO businesses
                      ("Id","Name","Slug","Category","IndustryProfileId","SubscriptionStatus","TrialStartDate","TrialEndDate","CreatedAt","UpdatedAt")
                    VALUES (@business,'Journal Test',@slug,'Test','ca100001-0000-4000-8000-000000000001','Active',now(),now()+interval '1 day',now(),now());
                    INSERT INTO outlets ("Id","BusinessId","Name","Code","IsActive","CreatedAt","UpdatedAt")
                    VALUES (@outlet,@business,'Journal Outlet',@code,true,now(),now());
                    INSERT INTO chart_of_accounts
                      ("Id","BusinessId","OutletId","AccountCode","AccountName","AccountType","IsCashBank","IsActive","CreatedAt","UpdatedAt")
                    VALUES
                      (@debit,@business,@outlet,'1010','Cash','asset',true,true,now(),now()),
                      (@credit,@business,@outlet,'4100','Revenue','revenue',false,true,now(),now());
                    """;
                seed.Parameters.AddWithValue("business", business);
                seed.Parameters.AddWithValue("outlet", outlet);
                seed.Parameters.AddWithValue("debit", debitAccount);
                seed.Parameters.AddWithValue("credit", creditAccount);
                seed.Parameters.AddWithValue("slug", $"journal-{suffix}");
                seed.Parameters.AddWithValue("code", $"JRN-{suffix}");
                await seed.ExecuteNonQueryAsync();
            }

            await using var app = new NpgsqlConnection(appCs);
            await app.OpenAsync();
            await SetBusinessAsync(app, business);
            var journalId = Guid.NewGuid();
            await using (var tx = await app.BeginTransactionAsync())
            {
                await InsertHeaderAsync(app, tx, journalId, business, outlet, journalId, 100);
                await InsertLineAsync(app, tx, business, outlet, journalId, debitAccount, 1, 100, 0);
                await InsertLineAsync(app, tx, business, outlet, journalId, creditAccount, 2, 0, 100);
                await tx.CommitAsync();
            }

            await using (var immutable = app.CreateCommand())
            {
                immutable.CommandText = "UPDATE journal_entries SET \"Description\"='changed' WHERE \"Id\"=@id;";
                immutable.Parameters.AddWithValue("id", journalId);
                var ex = await Assert.ThrowsAsync<PostgresException>(() => immutable.ExecuteNonQueryAsync());
                Assert.Contains(ex.SqlState, new[] { PostgresErrorCodes.InsufficientPrivilege, PostgresErrorCodes.RaiseException });
            }

            await using (var mark = app.CreateCommand())
            {
                mark.CommandText = "SELECT fn_mark_journal_reversed(@id);";
                mark.Parameters.AddWithValue("id", journalId);
                Assert.Equal(journalId, (Guid)(await mark.ExecuteScalarAsync())!);
            }
            await using (var status = app.CreateCommand())
            {
                status.CommandText = "SELECT \"Status\" FROM journal_entries WHERE \"Id\"=@id;";
                status.Parameters.AddWithValue("id", journalId);
                Assert.Equal("reversed", (string)(await status.ExecuteScalarAsync())!);
            }
            await using (var again = app.CreateCommand())
            {
                again.CommandText = "SELECT fn_mark_journal_reversed(@id);";
                again.Parameters.AddWithValue("id", journalId);
                var ex = await Assert.ThrowsAsync<PostgresException>(() => again.ExecuteScalarAsync());
                Assert.StartsWith("JOURNAL_REVERSAL_INVALID", ex.MessageText);
            }

            await SetBusinessAsync(app, Guid.NewGuid());
            await using (var wrongTenant = app.CreateCommand())
            {
                wrongTenant.CommandText = "SELECT fn_mark_journal_reversed(@id);";
                wrongTenant.Parameters.AddWithValue("id", journalId);
                var ex = await Assert.ThrowsAsync<PostgresException>(() => wrongTenant.ExecuteScalarAsync());
                Assert.StartsWith("JOURNAL_REVERSAL_INVALID", ex.MessageText);
            }
            await SetBusinessAsync(app, business);

            await using (var tx = await app.BeginTransactionAsync())
            {
                var badJournal = Guid.NewGuid();
                await InsertHeaderAsync(app, tx, badJournal, business, outlet, badJournal, 100);
                await InsertLineAsync(app, tx, business, outlet, badJournal, debitAccount, 1, 100, 0);
                await InsertLineAsync(app, tx, business, outlet, badJournal, creditAccount, 2, 0, 90);
                var ex = await Assert.ThrowsAsync<PostgresException>(() => tx.CommitAsync());
                Assert.Equal(PostgresErrorCodes.RaiseException, ex.SqlState);
            }
        }
        finally
        {
            await using var cleanup = admin.CreateCommand();
            cleanup.CommandText = """
                BEGIN;
                SET LOCAL session_replication_role = replica;
                DELETE FROM account_transactions WHERE "BusinessId"=@business;
                DELETE FROM journal_entries WHERE "BusinessId"=@business;
                DELETE FROM chart_of_accounts WHERE "BusinessId"=@business;
                DELETE FROM outlets WHERE "BusinessId"=@business;
                DELETE FROM businesses WHERE "Id"=@business;
                COMMIT;
                """;
            cleanup.Parameters.AddWithValue("business", business);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    private static async Task InsertHeaderAsync(NpgsqlConnection connection, NpgsqlTransaction tx, Guid id, Guid business, Guid outlet, Guid reference, decimal total)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = """
            INSERT INTO journal_entries
              ("Id","BusinessId","OutletId","JournalNumber","JournalDate","ReferenceType","ReferenceId","Description","Status","TotalDebit","TotalCredit","PostingHash","CreatedAt")
            VALUES (@id,@business,@outlet,@number,now(),'integration_test',@reference,'Test','posted',@total,@total,repeat('a',64),now());
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("business", business);
        command.Parameters.AddWithValue("outlet", outlet);
        command.Parameters.AddWithValue("number", $"JRN-{id:N}");
        command.Parameters.AddWithValue("reference", reference);
        command.Parameters.AddWithValue("total", total);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InsertLineAsync(NpgsqlConnection connection, NpgsqlTransaction tx, Guid business, Guid outlet, Guid journal, Guid account, int line, decimal debit, decimal credit)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = tx;
        command.CommandText = """
            INSERT INTO account_transactions
              ("Id","BusinessId","OutletId","JournalEntryId","LineNumber","TrxDate","TrxNumber","ReferenceType","ReferenceId","TrxEntity","ChartOfAccountId","DebitAmount","CreditAmount","Note","CreatedAt","UpdatedAt")
            VALUES (gen_random_uuid(),@business,@outlet,@journal,@line,now(),@number,'integration_test',@reference,'business',@account,@debit,@credit,'Test',now(),now());
            """;
        command.Parameters.AddWithValue("business", business);
        command.Parameters.AddWithValue("outlet", outlet);
        command.Parameters.AddWithValue("journal", journal);
        command.Parameters.AddWithValue("line", line);
        command.Parameters.AddWithValue("number", $"JRN-{journal:N}");
        command.Parameters.AddWithValue("reference", journal);
        command.Parameters.AddWithValue("account", account);
        command.Parameters.AddWithValue("debit", debit);
        command.Parameters.AddWithValue("credit", credit);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task SetBusinessAsync(NpgsqlConnection connection, Guid businessId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select set_config('app.business_id',@business,false);";
        command.Parameters.AddWithValue("business", businessId.ToString());
        await command.ExecuteNonQueryAsync();
    }
}
