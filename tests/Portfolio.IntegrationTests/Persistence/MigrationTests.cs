using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Portfolio.IntegrationTests.Infrastructure;
using Xunit;

namespace Portfolio.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class MigrationTests(PostgreSqlFixture database)
{
    private const string InitialMigration = "20260905063820_InitialCreate";

    [Fact]
    public async Task InitialAndCorrectionMigrationsApplyToEmptyPostgreSql()
    {
        await using var context = database.CreateDbContext();

        var applied = await context.Database.GetAppliedMigrationsAsync(
            TestContext.Current.CancellationToken);
        var pending = await context.Database.GetPendingMigrationsAsync(
            TestContext.Current.CancellationToken);

        Assert.Equal(3, applied.Count());
        Assert.Empty(pending);
    }

    [Fact]
    public async Task CorrectionDefaultsArePrivateAndDraftAtDatabaseBoundary()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            WITH profile AS (
                INSERT INTO profiles (slug, full_name)
                VALUES ('default-proof', 'Default Proof')
                RETURNING show_email, show_phone
            ), certificate AS (
                INSERT INTO certificates (issuer, issued_date)
                VALUES ('Issuer', DATE '2026-09-05')
                RETURNING show_credential_id, is_published
            )
            SELECT profile.show_email, profile.show_phone,
                   certificate.show_credential_id, certificate.is_published
            FROM profile CROSS JOIN certificate;
            """;

        await using var reader = await command.ExecuteReaderAsync(
            TestContext.Current.CancellationToken);
        Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
        Assert.False(reader.GetBoolean(0));
        Assert.False(reader.GetBoolean(1));
        Assert.False(reader.GetBoolean(2));
        Assert.False(reader.GetBoolean(3));
    }

    [Fact]
    public async Task CorrectionMigrationRejectsUnsafeCertificateNullState()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(
            InitialMigration,
            TestContext.Current.CancellationToken);

        try
        {
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO certificates (issuer, issued_date) VALUES ('Unsafe', NULL);",
                TestContext.Current.CancellationToken);

            var exception = await Assert.ThrowsAsync<PostgresException>(() =>
                migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken));

            Assert.Contains(
                "cannot require certificates.issued_date",
                exception.MessageText,
                StringComparison.Ordinal);
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync(
                "DELETE FROM certificates WHERE issued_date IS NULL;",
                TestContext.Current.CancellationToken);
            await migrator.MigrateAsync(
                cancellationToken: TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task RepresentativeRowsSurviveUpgradeFromInitialMigration()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(InitialMigration, TestContext.Current.CancellationToken);

        try
        {
            await context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO profiles (slug, full_name, email)
                VALUES ('migration-rehearsal', 'Migration Rehearsal', 'owner@example.test');
                INSERT INTO certificates (issuer, issued_date, is_published)
                VALUES ('Representative Issuer', DATE '2025-01-02', TRUE);
                """,
                TestContext.Current.CancellationToken);

            await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);

            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText =
                """
                SELECT p.show_email, p.show_phone, c.show_credential_id, c.is_published
                FROM profiles p CROSS JOIN certificates c
                WHERE p.slug = 'migration-rehearsal'
                  AND c.issuer = 'Representative Issuer';
                """;
            await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
            await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

            Assert.True(await reader.ReadAsync(TestContext.Current.CancellationToken));
            Assert.False(reader.GetBoolean(0));
            Assert.False(reader.GetBoolean(1));
            Assert.False(reader.GetBoolean(2));
            Assert.True(reader.GetBoolean(3));
        }
        finally
        {
            await migrator.MigrateAsync(cancellationToken: TestContext.Current.CancellationToken);
        }
    }
}
