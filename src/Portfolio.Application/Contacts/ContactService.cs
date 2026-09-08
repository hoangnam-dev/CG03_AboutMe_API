using Microsoft.Extensions.Logging;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Models;

namespace Portfolio.Application.Contacts;

public sealed partial class ContactService(
    IContactRepository repository,
    IContactNotifier notifier,
    TimeProvider timeProvider,
    ILogger<ContactService> logger) : IContactService
{
    public async Task<ContactReceiptResponse> CreateContactAsync(
        ContactCreateRequest request,
        ContactSubmissionMetadata metadata,
        CancellationToken cancellationToken)
    {
        ValidateCreate(request);

        var now = timeProvider.GetUtcNow();
        var message = new ContactMessage
        {
            Id = Guid.NewGuid(),
            SenderName = request.SenderName.Trim(),
            SenderEmail = request.SenderEmail.Trim(),
            Subject = request.Subject.Trim(),
            Message = request.Message.Trim(),
            Status = ContactStatus.New,
            IsSpam = !string.IsNullOrWhiteSpace(request.Website),
            IpHash = NullIfWhiteSpace(metadata.IpHash),
            UserAgent = Truncate(NullIfWhiteSpace(metadata.UserAgent), 500),
            CreatedAt = now,
        };

        await repository.AddAsync(message, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        ContactSaved(logger, message.Id, message.IsSpam);

        if (!message.IsSpam)
        {
            try
            {
                await notifier.NotifyAsync(message, cancellationToken);
            }
            catch (Exception)
            {
                NotificationFailed(logger, message.Id);
            }
        }

        return new ContactReceiptResponse(message.Id, message.CreatedAt);
    }

    public async Task<ContactAdminPage> GetContactsAsync(
        ContactAdminQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Page < 1)
            throw Invalid("page", "Page must be at least 1.");
        if (query.PageSize is < 1 or > 100)
            throw Invalid("pageSize", "Page size must be between 1 and 100.");
        if (query.Status.HasValue && !Enum.IsDefined(query.Status.Value))
            throw Invalid("status", "Status must be New, Read, or Archived.");
        if (query.Search?.Trim().Length > 200)
            throw Invalid("search", "Search must not exceed 200 characters.");

        var normalized = query with { Search = NullIfWhiteSpace(query.Search) };
        var page = await repository.GetPageAsync(normalized, cancellationToken);
        return new ContactAdminPage(
            page.Items.Select(Map).ToArray(),
            page.Total,
            page.Page,
            page.PageSize);
    }

    public async Task<ContactAdminResponse> GetContactAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var message = await repository.GetAsync(id, false, cancellationToken)
            ?? throw new NotFoundException("Contact message was not found.");
        return Map(message);
    }

    public async Task<ContactAdminResponse> UpdateStatusAsync(
        Guid id,
        ContactStatusRequest request,
        CancellationToken cancellationToken)
    {
        if (!request.Status.HasValue || !Enum.IsDefined(request.Status.Value))
            throw Invalid("status", "Status must be New, Read, or Archived.");

        var message = await repository.GetAsync(id, true, cancellationToken)
            ?? throw new NotFoundException("Contact message was not found.");
        message.Status = request.Status.Value;
        if (request.Status.Value == ContactStatus.Read)
            message.ReadAt ??= timeProvider.GetUtcNow();
        else if (request.Status.Value == ContactStatus.New)
            message.ReadAt = null;

        await repository.SaveChangesAsync(cancellationToken);
        StatusChanged(logger, message.Id, message.Status);
        return Map(message);
    }

    public async Task DeleteContactAsync(Guid id, CancellationToken cancellationToken)
    {
        var message = await repository.GetAsync(id, true, cancellationToken)
            ?? throw new NotFoundException("Contact message was not found.");
        repository.Remove(message);
        await repository.SaveChangesAsync(cancellationToken);
        ContactDeleted(logger, id);
    }

    private static ContactAdminResponse Map(ContactMessage message) => new(
        message.Id,
        message.SenderName,
        message.SenderEmail,
        message.Subject,
        message.Message,
        message.Status,
        message.IsSpam,
        message.IpHash,
        message.UserAgent,
        message.CreatedAt,
        message.ReadAt);

    private static void ValidateCreate(ContactCreateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SenderName) || request.SenderName.Trim().Length > 150)
            throw Invalid("senderName", "Sender name is required and must not exceed 150 characters.");
        if (string.IsNullOrWhiteSpace(request.SenderEmail) ||
            request.SenderEmail.Trim().Length > 320 ||
            !new System.ComponentModel.DataAnnotations.EmailAddressAttribute()
                .IsValid(request.SenderEmail.Trim()))
            throw Invalid("senderEmail", "Sender email must be a valid email address and must not exceed 320 characters.");
        if (string.IsNullOrWhiteSpace(request.Subject) || request.Subject.Trim().Length > 200)
            throw Invalid("subject", "Subject is required and must not exceed 200 characters.");
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Trim().Length > 5000)
            throw Invalid("message", "Message is required and must not exceed 5000 characters.");
        if (request.Website?.Trim().Length > 200)
            throw Invalid("website", "Website must not exceed 200 characters.");
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Truncate(string? value, int maximumLength) =>
        value?.Length > maximumLength ? value[..maximumLength] : value;

    private static ValidationException Invalid(string field, string message) =>
        new("Contact validation failed.", new Dictionary<string, string[]> { [field] = [message] });

    [LoggerMessage(EventId = 2801, Level = LogLevel.Information, Message = "Contact message {ContactId} was saved. Spam: {IsSpam}.")]
    private static partial void ContactSaved(ILogger logger, Guid contactId, bool isSpam);

    [LoggerMessage(EventId = 2802, Level = LogLevel.Warning, Message = "Contact notification failed for message {ContactId}; the persisted message remains available.")]
    private static partial void NotificationFailed(ILogger logger, Guid contactId);

    [LoggerMessage(EventId = 2803, Level = LogLevel.Information, Message = "Contact message {ContactId} status changed to {Status}.")]
    private static partial void StatusChanged(ILogger logger, Guid contactId, ContactStatus status);

    [LoggerMessage(EventId = 2804, Level = LogLevel.Information, Message = "Contact message {ContactId} was deleted.")]
    private static partial void ContactDeleted(ILogger logger, Guid contactId);
}
