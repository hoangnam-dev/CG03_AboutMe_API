using Portfolio.Application.Common.Models;
using Portfolio.Application.Contacts;
using Portfolio.Infrastructure.Persistence.Repositories;
using Portfolio.IntegrationTests.Infrastructure;
using Xunit;

namespace Portfolio.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class ContactRepositoryTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task AdminListFiltersSearchesAndOrdersNewestFirst()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        var oldestMatch = Message("Visitor One", "one@example.com", ContactStatus.New, 1);
        var newestMatch = Message("Other", "visitor@example.com", ContactStatus.New, 3);
        var wrongStatus = Message("Visitor Read", "read@example.com", ContactStatus.Read, 4);
        var wrongSearch = Message("Different", "different@example.com", ContactStatus.New, 5);
        context.ContactMessages.AddRange(oldestMatch, newestMatch, wrongStatus, wrongSearch);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new ContactRepository(context);

        var result = await repository.GetPageAsync(
            new ContactAdminQuery(1, 20, ContactStatus.New, "visitor"),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Total);
        Assert.Equal([newestMatch.Id, oldestMatch.Id], result.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task AdminListPaginatesWithStableCreatedDescendingIdOrder()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        var timestamp = new DateTimeOffset(2026, 9, 8, 8, 0, 0, TimeSpan.Zero);
        var firstId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var secondId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        context.ContactMessages.AddRange(
            Message("First", "first@example.com", ContactStatus.New, 1, firstId, timestamp),
            Message("Second", "second@example.com", ContactStatus.New, 1, secondId, timestamp));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var repository = new ContactRepository(context);

        var result = await repository.GetPageAsync(
            new ContactAdminQuery(2, 1, null, null),
            TestContext.Current.CancellationToken);

        Assert.Equal(2, result.Total);
        Assert.Equal(2, result.Page);
        Assert.Equal(secondId, Assert.Single(result.Items).Id);
    }

    [Fact]
    public async Task AddUpdateAndDeletePersistContactMessage()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        var repository = new ContactRepository(context);
        var message = Message("Visitor", "visitor@example.com", ContactStatus.New, 1);

        await repository.AddAsync(message, TestContext.Current.CancellationToken);
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();
        var tracked = await repository.GetAsync(
            message.Id, true, TestContext.Current.CancellationToken);
        tracked!.Status = ContactStatus.Read;
        tracked.ReadAt = new DateTimeOffset(2026, 9, 8, 9, 0, 0, TimeSpan.Zero);
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);
        repository.Remove(tracked);
        await repository.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.ChangeTracker.Clear();
        Assert.Null(await repository.GetAsync(
            message.Id, false, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DashboardUnreadCountTracksNewStatusAndDeletion()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        var unread = Message("Unread", "unread@example.com", ContactStatus.New, 1);
        var read = Message("Read", "read@example.com", ContactStatus.Read, 2);
        context.ContactMessages.AddRange(unread, read);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var contactRepository = new ContactRepository(context);
        var dashboardRepository = new DashboardRepository(context);

        Assert.Equal(1, (await dashboardRepository.GetStatsAsync(
            TestContext.Current.CancellationToken)).UnreadContacts);
        contactRepository.Remove(unread);
        await contactRepository.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, (await dashboardRepository.GetStatsAsync(
            TestContext.Current.CancellationToken)).UnreadContacts);
    }

    private static ContactMessage Message(
        string name,
        string email,
        ContactStatus status,
        int hour,
        Guid? id = null,
        DateTimeOffset? createdAt = null) => new()
        {
            Id = id ?? Guid.NewGuid(),
            SenderName = name,
            SenderEmail = email,
            Subject = "Project inquiry",
            Message = "Plain text message.",
            Status = status,
            CreatedAt = createdAt ?? new DateTimeOffset(2026, 9, 8, hour, 0, 0, TimeSpan.Zero),
        };
}
