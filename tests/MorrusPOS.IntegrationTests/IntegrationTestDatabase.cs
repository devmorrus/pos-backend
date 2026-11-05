using Npgsql;

namespace MorrusPOS.IntegrationTests;

internal static class IntegrationTestDatabase
{
    public static string RequireConnectionString(string variable)
        => Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException(
                $"{variable} wajib diisi untuk menjalankan integration test PostgreSQL. Lihat docs/automated_testing_guide.md.");

    public static async Task CleanupBusinessAsync(Guid businessId)
    {
        await using var connection = new NpgsqlConnection(RequireConnectionString("MORRUS_TENANCY_TEST_ADMIN"));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            BEGIN;
            SET LOCAL session_replication_role = replica;
            SELECT set_config('morrus.test_business_id', @business_id, true);
            DO $cleanup$
            DECLARE tenant_table record;
            BEGIN
                FOR tenant_table IN
                    SELECT format('%I.%I', n.nspname, c.relname) AS table_name
                    FROM pg_class c
                    JOIN pg_namespace n ON n.oid = c.relnamespace
                    WHERE c.relkind = 'r'
                      AND n.nspname = 'public'
                      AND c.relname <> 'businesses'
                      AND EXISTS (
                          SELECT 1 FROM pg_attribute a
                          WHERE a.attrelid = c.oid
                            AND a.attname = 'BusinessId'
                            AND NOT a.attisdropped)
                LOOP
                    EXECUTE format(
                        'DELETE FROM %s WHERE "BusinessId" = %L::uuid',
                        tenant_table.table_name,
                        current_setting('morrus.test_business_id'));
                END LOOP;
            END
            $cleanup$;
            DELETE FROM businesses WHERE "Id" = @business_id::uuid;
            COMMIT;
            """;
        command.Parameters.AddWithValue("business_id", businessId.ToString());
        await command.ExecuteNonQueryAsync();
    }
}
