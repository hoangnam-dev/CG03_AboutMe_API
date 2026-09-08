using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Contacts;

namespace Portfolio.Infrastructure.Availability;

internal sealed class DatabaseUnavailableContactRepository : IContactRepository
{
    private static ServiceUnavailableException Unavailable() =>
        new("Contact data is unavailable because PostgreSQL is not configured.");

    public Task<ContactMessagePage> GetPageAsync(ContactAdminQuery query, CancellationToken cancellationToken) =>
        throw Unavailable();

    public Task<ContactMessage?> GetAsync(Guid id, bool tracked, CancellationToken cancellationToken) =>
        throw Unavailable();

    public Task AddAsync(ContactMessage message, CancellationToken cancellationToken) =>
        throw Unavailable();

    public void Remove(ContactMessage message) => throw Unavailable();

    public Task SaveChangesAsync(CancellationToken cancellationToken) => throw Unavailable();
}
