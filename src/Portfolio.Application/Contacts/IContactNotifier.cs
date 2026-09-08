using Portfolio.Application.Common.Models;

namespace Portfolio.Application.Contacts;

public interface IContactNotifier
{
    Task NotifyAsync(ContactMessage message, CancellationToken cancellationToken);
}
