using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Contacts;
using Xunit;

namespace Portfolio.UnitTests.Contacts;

public sealed class ContactServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 8, 8, 30, 0, TimeSpan.Zero);

    [Theory]
    [MemberData(nameof(InvalidRequests))]
    public async Task CreateRejectsInvalidFields(ContactCreateRequest request, string field)
    {
        var service = CreateService(new ContactRepositoryStub(), new RecordingNotifier());

        var error = await Assert.ThrowsAsync<ValidationException>(() => service.CreateContactAsync(
            request,
            new ContactSubmissionMetadata("ip-hash", "test-agent"),
            TestContext.Current.CancellationToken));

        Assert.Contains(field, error.Errors.Keys);
    }

    [Fact]
    public async Task CreateTreatsFilledHoneypotAsSpamWithoutChangingReceiptShape()
    {
        var repository = new ContactRepositoryStub();
        var notifier = new RecordingNotifier();
        var service = CreateService(repository, notifier);

        var result = await service.CreateContactAsync(
            ValidRequest() with { Website = "https://spam.invalid" },
            new ContactSubmissionMetadata("ip-hash", "test-agent"),
            TestContext.Current.CancellationToken);

        Assert.NotNull(repository.Added);
        Assert.True(repository.Added.IsSpam);
        Assert.Equal(repository.Added.Id, result.Id);
        Assert.Equal(repository.Added.CreatedAt, result.ReceivedAt);
        Assert.Empty(notifier.Notified);
    }

    [Fact]
    public async Task NotificationFailureDoesNotLosePersistedMessage()
    {
        var repository = new ContactRepositoryStub();
        var notifier = new ThrowingNotifier(repository);
        var service = CreateService(repository, notifier);

        var result = await service.CreateContactAsync(
            ValidRequest(),
            new ContactSubmissionMetadata("ip-hash", "test-agent"),
            TestContext.Current.CancellationToken);

        Assert.True(repository.Saved);
        Assert.True(notifier.RepositoryWasSaved);
        Assert.Equal(repository.Added!.Id, result.Id);
    }

    [Fact]
    public async Task NotificationFailureLogContainsNoSubmittedPiiOrProviderException()
    {
        var repository = new ContactRepositoryStub();
        var logger = new RecordingLogger<ContactService>();
        var request = ValidRequest();
        var service = new ContactService(
            repository,
            new PiiThrowingNotifier(request),
            new FixedTimeProvider(Now),
            logger);

        await service.CreateContactAsync(
            request,
            new ContactSubmissionMetadata("ip-hash", "test-agent"),
            TestContext.Current.CancellationToken);

        Assert.DoesNotContain(logger.Entries, entry =>
            entry.Message.Contains(request.SenderEmail, StringComparison.Ordinal) ||
            entry.Message.Contains(request.Message, StringComparison.Ordinal));
        Assert.All(logger.Entries, entry => Assert.Null(entry.Exception));
    }

    [Fact]
    public async Task CreateTrimsFieldsAndBoundsUserAgent()
    {
        var repository = new ContactRepositoryStub();
        var service = CreateService(repository, new RecordingNotifier());

        await service.CreateContactAsync(
            ValidRequest() with
            {
                SenderName = "  Fictional Visitor  ",
                Subject = "  Inquiry  ",
                Message = "  Hello  ",
            },
            new ContactSubmissionMetadata("ip-hash", new string('a', 600)),
            TestContext.Current.CancellationToken);

        Assert.Equal("Fictional Visitor", repository.Added!.SenderName);
        Assert.Equal("Inquiry", repository.Added.Subject);
        Assert.Equal("Hello", repository.Added.Message);
        Assert.Equal(500, repository.Added.UserAgent!.Length);
        Assert.Equal(ContactStatus.New, repository.Added.Status);
        Assert.Equal(Now, repository.Added.CreatedAt);
    }

    [Fact]
    public async Task MarkReadSetsReadAt()
    {
        var message = ExistingMessage(ContactStatus.New);
        var repository = new ContactRepositoryStub(message);
        var service = CreateService(repository, new RecordingNotifier());

        var result = await service.UpdateStatusAsync(
            message.Id,
            new ContactStatusRequest(ContactStatus.Read),
            TestContext.Current.CancellationToken);

        Assert.Equal(ContactStatus.Read, result.Status);
        Assert.Equal(Now, message.ReadAt);
        Assert.True(repository.Saved);
    }

    [Fact]
    public async Task ArchiveDoesNotRewriteReadAt()
    {
        var originalReadAt = Now.AddDays(-1);
        var message = ExistingMessage(ContactStatus.Read).WithReadAt(originalReadAt);
        var service = CreateService(new ContactRepositoryStub(message), new RecordingNotifier());

        var result = await service.UpdateStatusAsync(
            message.Id,
            new ContactStatusRequest(ContactStatus.Archived),
            TestContext.Current.CancellationToken);

        Assert.Equal(ContactStatus.Archived, result.Status);
        Assert.Equal(originalReadAt, result.ReadAt);
    }

    [Fact]
    public async Task ReturningToNewClearsReadAt()
    {
        var message = ExistingMessage(ContactStatus.Read).WithReadAt(Now.AddDays(-1));
        var service = CreateService(new ContactRepositoryStub(message), new RecordingNotifier());

        var result = await service.UpdateStatusAsync(
            message.Id,
            new ContactStatusRequest(ContactStatus.New),
            TestContext.Current.CancellationToken);

        Assert.Equal(ContactStatus.New, result.Status);
        Assert.Null(result.ReadAt);
    }

    [Fact]
    public async Task GetMissingContactThrowsNotFound()
    {
        var service = CreateService(new ContactRepositoryStub(), new RecordingNotifier());

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetContactAsync(
            Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetContactIncludesStoredIpHashForAdministratorReview()
    {
        var message = ExistingMessage(ContactStatus.New);
        message.IpHash = "privacy-preserving-hash";
        var service = CreateService(new ContactRepositoryStub(message), new RecordingNotifier());

        var result = await service.GetContactAsync(
            message.Id, TestContext.Current.CancellationToken);

        Assert.Equal("privacy-preserving-hash", result.IpHash);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task ListRejectsInvalidPagination(int page, int pageSize)
    {
        var service = CreateService(new ContactRepositoryStub(), new RecordingNotifier());

        await Assert.ThrowsAsync<ValidationException>(() => service.GetContactsAsync(
            new ContactAdminQuery(page, pageSize, null, null),
            TestContext.Current.CancellationToken));
    }

    public static TheoryData<ContactCreateRequest, string> InvalidRequests => new()
    {
        { ValidRequest() with { SenderName = " " }, "senderName" },
        { ValidRequest() with { SenderName = new string('a', 151) }, "senderName" },
        { ValidRequest() with { SenderEmail = "invalid" }, "senderEmail" },
        { ValidRequest() with { SenderEmail = new string('a', 309) + "@example.com" }, "senderEmail" },
        { ValidRequest() with { Subject = " " }, "subject" },
        { ValidRequest() with { Subject = new string('a', 201) }, "subject" },
        { ValidRequest() with { Message = " " }, "message" },
        { ValidRequest() with { Message = new string('a', 5001) }, "message" },
        { ValidRequest() with { Website = new string('a', 201) }, "website" },
    };

    private static ContactService CreateService(
        IContactRepository repository,
        IContactNotifier notifier) =>
        new(repository, notifier, new FixedTimeProvider(Now), NullLogger<ContactService>.Instance);

    private static ContactCreateRequest ValidRequest() => new(
        "Fictional Visitor",
        "visitor@example.com",
        "Project inquiry",
        "Plain text message.",
        string.Empty);

    private static ContactMessage ExistingMessage(ContactStatus status) => new()
    {
        Id = Guid.NewGuid(),
        SenderName = "Fictional Visitor",
        SenderEmail = "visitor@example.com",
        Subject = "Project inquiry",
        Message = "Plain text message.",
        Status = status,
        CreatedAt = Now.AddDays(-2),
    };

    private sealed class ContactRepositoryStub(ContactMessage? existing = null) : IContactRepository
    {
        public ContactMessage? Added { get; private set; }
        public bool Saved { get; private set; }

        public Task<ContactMessagePage> GetPageAsync(ContactAdminQuery query, CancellationToken cancellationToken) =>
            Task.FromResult(new ContactMessagePage([], 0, query.Page, query.PageSize));

        public Task<ContactMessage?> GetAsync(Guid id, bool tracked, CancellationToken cancellationToken) =>
            Task.FromResult(existing?.Id == id ? existing : null);

        public Task AddAsync(ContactMessage message, CancellationToken cancellationToken)
        {
            Added = message;
            return Task.CompletedTask;
        }

        public void Remove(ContactMessage message)
        {
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Saved = true;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingNotifier : IContactNotifier
    {
        public List<Guid> Notified { get; } = [];

        public Task NotifyAsync(ContactMessage message, CancellationToken cancellationToken)
        {
            Notified.Add(message.Id);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingNotifier(ContactRepositoryStub repository) : IContactNotifier
    {
        public bool RepositoryWasSaved { get; private set; }

        public Task NotifyAsync(ContactMessage message, CancellationToken cancellationToken)
        {
            RepositoryWasSaved = repository.Saved;
            throw new InvalidOperationException("Provider unavailable.");
        }
    }

    private sealed class PiiThrowingNotifier(ContactCreateRequest request) : IContactNotifier
    {
        public Task NotifyAsync(ContactMessage message, CancellationToken cancellationToken) =>
            throw new InvalidOperationException($"Failed for {request.SenderEmail}: {request.Message}");
    }

    private sealed class RecordingLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add(new LogEntry(formatter(state, exception), exception));
    }

    private sealed record LogEntry(string Message, Exception? Exception);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

file static class ContactMessageTestExtensions
{
    public static ContactMessage WithReadAt(this ContactMessage message, DateTimeOffset readAt)
    {
        message.ReadAt = readAt;
        return message;
    }
}
