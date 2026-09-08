using Microsoft.Extensions.Logging;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Contacts;

namespace Portfolio.Infrastructure.Notifications;

public sealed partial class LoggingContactNotifier(
    ILogger<LoggingContactNotifier> logger) : IContactNotifier
{
    public Task NotifyAsync(ContactMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        NotificationSkipped(logger, message.Id);
        return Task.CompletedTask;
    }

    [LoggerMessage(
        EventId = 3801,
        Level = LogLevel.Information,
        Message = "Contact notification provider is not configured; message {ContactId} remains in the administrator inbox.")]
    private static partial void NotificationSkipped(ILogger logger, Guid contactId);
}
