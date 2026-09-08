using Portfolio.Application.Common.Models;

namespace Portfolio.Application.Contacts;

public interface IContactRepository
{
    Task<ContactMessagePage> GetPageAsync(
        ContactAdminQuery query,
        CancellationToken cancellationToken);

    Task<ContactMessage?> GetAsync(
        Guid id,
        bool tracked,
        CancellationToken cancellationToken);

    Task AddAsync(ContactMessage message, CancellationToken cancellationToken);

    void Remove(ContactMessage message);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
