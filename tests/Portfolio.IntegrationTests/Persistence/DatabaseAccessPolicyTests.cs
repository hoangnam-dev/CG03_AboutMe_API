using Npgsql;
using Portfolio.IntegrationTests.Infrastructure;
using Xunit;

namespace Portfolio.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class DatabaseAccessPolicyTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task BootstrapGrantAllowsNonSuperuserRoleCreatorToAlterMigratorDefaults()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var bootstrapRole = $"policy_bootstrap_{suffix}";
        var migratorRole = $"policy_migrator_{suffix}";
        const string bootstrapPassword = "policy-test-only";

        await using var adminConnection = new NpgsqlConnection(database.ConnectionString);
        await adminConnection.OpenAsync(TestContext.Current.CancellationToken);
        await ExecuteAsync(
            adminConnection,
            $"""
            CREATE ROLE {bootstrapRole}
                LOGIN CREATEROLE NOSUPERUSER NOCREATEDB NOREPLICATION
                PASSWORD '{bootstrapPassword}';
            """);

        try
        {
            var bootstrapConnectionString =
                new NpgsqlConnectionStringBuilder(database.ConnectionString)
                {
                    Username = bootstrapRole,
                    Password = bootstrapPassword
                }.ConnectionString;

            await using var bootstrapConnection =
                new NpgsqlConnection(bootstrapConnectionString);
            await bootstrapConnection.OpenAsync(TestContext.Current.CancellationToken);
            await ExecuteAsync(
                bootstrapConnection,
                $"CREATE ROLE {migratorRole} NOLOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION;");

            var apply = await ReadSqlAsync("database-access.sql");
            var bootstrapGrant = apply.ReplaceLineEndings("\n").Split('\n')
                .Single(line => line.TrimStart().StartsWith(
                    "GRANT portfolio_migrator TO CURRENT_USER",
                    StringComparison.OrdinalIgnoreCase))
                .Trim()
                .Replace("portfolio_migrator", migratorRole, StringComparison.Ordinal);

            await ExecuteAsync(bootstrapConnection, bootstrapGrant);
            await ExecuteAsync(bootstrapConnection, $"SET ROLE {migratorRole};");
            try
            {
                await ExecuteAsync(
                    bootstrapConnection,
                    $"""
                    ALTER DEFAULT PRIVILEGES IN SCHEMA public
                        GRANT SELECT ON TABLES TO {bootstrapRole};
                    """);
            }
            finally
            {
                await ExecuteAsync(bootstrapConnection, "RESET ROLE;");
            }
        }
        finally
        {
            await ExecuteAsync(
                adminConnection,
                $"""
                DROP OWNED BY {migratorRole};
                DROP OWNED BY {bootstrapRole};
                DROP ROLE IF EXISTS {migratorRole};
                DROP ROLE IF EXISTS {bootstrapRole};
                """);
        }
    }

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
            "GRANT portfolio_migrator TO CURRENT_USER WITH SET TRUE, INHERIT FALSE;",
            StringComparison.OrdinalIgnoreCase);
        var migrationRoleActivation = apply.IndexOf(
            "SET LOCAL ROLE portfolio_migrator;",
            StringComparison.OrdinalIgnoreCase);
        var migrationDefaultPrivileges = apply.IndexOf(
            "ALTER DEFAULT PRIVILEGES IN SCHEMA public",
            StringComparison.OrdinalIgnoreCase);

        Assert.True(
            migrationMembershipGrant >= 0 &&
            migrationMembershipGrant < migrationRoleActivation &&
            migrationRoleActivation < migrationDefaultPrivileges,
            "The bootstrap identity must be able to SET ROLE to " +
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
