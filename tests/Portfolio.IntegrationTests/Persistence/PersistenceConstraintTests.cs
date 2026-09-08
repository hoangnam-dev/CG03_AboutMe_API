using Microsoft.EntityFrameworkCore;
using Npgsql;
using Portfolio.Application.Common.Models;
using Portfolio.IntegrationTests.Infrastructure;
using Xunit;

namespace Portfolio.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class PersistenceConstraintTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task RejectsUnsupportedLocale()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        var profile = new Profile { Slug = "locale-proof", FullName = "Locale Proof" };
        profile.Translations.Add(new ProfileTranslation
        {
            LocaleCode = "fr",
            Title = "Unsupported",
        });
        context.Profiles.Add(profile);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            context.SaveChangesAsync(TestContext.Current.CancellationToken));

        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.CheckViolation, postgres.SqlState);
        Assert.Equal("ck_profile_translations_locale", postgres.ConstraintName);
    }

    [Fact]
    public async Task RejectsNegativeDisplayOrder()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        var profile = new Profile { Slug = "order-proof", FullName = "Order Proof" };
        profile.SocialLinks.Add(new SocialLink
        {
            Platform = "github",
            Url = "https://github.com/example",
            DisplayOrder = -1,
        });
        context.Profiles.Add(profile);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() =>
            context.SaveChangesAsync(TestContext.Current.CancellationToken));

        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.CheckViolation, postgres.SqlState);
        Assert.Equal("ck_social_links_display_order", postgres.ConstraintName);
    }

    [Fact]
    public async Task RejectsUnsupportedContactStatus()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();

        var exception = await Assert.ThrowsAsync<PostgresException>(() =>
            context.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO contact_messages
                    (sender_name, sender_email, subject, message, status)
                VALUES
                    ('Fictional Visitor', 'visitor@example.com', 'Inquiry', 'Hello', 'deleted');
                """,
                TestContext.Current.CancellationToken));

        Assert.Equal(PostgresErrorCodes.CheckViolation, exception.SqlState);
        Assert.Equal("ck_contact_messages_status", exception.ConstraintName);
    }
}
