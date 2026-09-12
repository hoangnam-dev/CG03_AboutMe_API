using Npgsql;
using Portfolio.IntegrationTests.Infrastructure;
using Xunit;

namespace Portfolio.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class DatabaseAccessPolicyTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task AccessPolicyDeniesDataApiRolesAndGrantsRuntimeDml()
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await ExecuteAsync(
            connection,
            """
            DO $roles$
            BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN
                    CREATE ROLE anon NOLOGIN;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
                    CREATE ROLE authenticated NOLOGIN;
                END IF;
            END
            $roles$;
            """);

        var apply = await ReadSqlAsync("database-access.sql");
        var rollback = await ReadSqlAsync("database-access-rollback.sql");

        var migrationMembershipGrant = apply.IndexOf(
            "GRANT portfolio_migrator TO CURRENT_USER WITH ADMIN OPTION;",
            StringComparison.OrdinalIgnoreCase);
        var migrationDefaultPrivileges = apply.IndexOf(
            "ALTER DEFAULT PRIVILEGES FOR ROLE portfolio_migrator",
            StringComparison.OrdinalIgnoreCase);

        Assert.True(
            migrationMembershipGrant >= 0 &&
            migrationMembershipGrant < migrationDefaultPrivileges,
            "The bootstrap identity must become an administrator/member of " +
            "portfolio_migrator before changing that role's default privileges.");

        try
        {
            await ExecuteAsync(connection, apply);

            await using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    """
                    SELECT
                        has_schema_privilege('anon', 'public', 'USAGE'),
                        has_schema_privilege('authenticated', 'public', 'USAGE'),
                        has_table_privilege('portfolio_api', 'public.projects', 'SELECT'),
                        has_table_privilege('portfolio_api', 'public.projects', 'INSERT'),
                        has_table_privilege('portfolio_api', 'public.projects', 'UPDATE'),
                        has_table_privilege('portfolio_api', 'public.projects', 'DELETE'),
                        has_table_privilege('portfolio_api', 'public.projects', 'TRUNCATE'),
                        has_table_privilege('portfolio_api', 'public."__EFMigrationsHistory"', 'SELECT');
                    """;
                await using var reader = await command.ExecuteReaderAsync(
                    TestContext.Current.CancellationToken);

                Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
                Assert.False(reader.GetBoolean(0));
                Assert.False(reader.GetBoolean(1));
                Assert.True(reader.GetBoolean(2));
                Assert.True(reader.GetBoolean(3));
                Assert.True(reader.GetBoolean(4));
                Assert.True(reader.GetBoolean(5));
                Assert.False(reader.GetBoolean(6));
                Assert.False(reader.GetBoolean(7));
            }
        }
        finally
        {
            await ExecuteAsync(connection, rollback);
        }
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<string> ReadSqlAsync(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Security", fileName);
        var sql = await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken);
        return string.Join(
            Environment.NewLine,
            sql.ReplaceLineEndings("\n").Split('\n')
                .Where(line => !line.TrimStart().StartsWith('\\')));
    }
}
