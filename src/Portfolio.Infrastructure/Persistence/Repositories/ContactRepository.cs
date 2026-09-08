using Microsoft.EntityFrameworkCore;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Contacts;

namespace Portfolio.Infrastructure.Persistence.Repositories;

public sealed class ContactRepository(PortfolioDbContext context) : IContactRepository
{
    public async Task<ContactMessagePage> GetPageAsync(
        ContactAdminQuery query,
        CancellationToken cancellationToken)
    {
        var source = context.ContactMessages.AsNoTracking().AsQueryable();
        if (query.Status.HasValue)
            source = source.Where(message => message.Status == query.Status.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search}%";
            source = source.Where(message =>
                EF.Functions.ILike(message.SenderName, pattern) ||
                EF.Functions.ILike(message.SenderEmail, pattern) ||
                EF.Functions.ILike(message.Subject, pattern));
        }

        var total = await source.LongCountAsync(cancellationToken);
        var items = await source
            .OrderByDescending(message => message.CreatedAt)
            .ThenBy(message => message.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToArrayAsync(cancellationToken);
        return new ContactMessagePage(items, total, query.Page, query.PageSize);
    }

    public Task<ContactMessage?> GetAsync(
        Guid id,
        bool tracked,
        CancellationToken cancellationToken)
    {
        IQueryable<ContactMessage> query = context.ContactMessages;
        if (!tracked)
            query = query.AsNoTracking();
        return query.SingleOrDefaultAsync(message => message.Id == id, cancellationToken);
    }

    public async Task AddAsync(
        ContactMessage message,
        CancellationToken cancellationToken) =>
        await context.ContactMessages.AddAsync(message, cancellationToken);

    public void Remove(ContactMessage message) => context.ContactMessages.Remove(message);

    public async Task SaveChangesAsync(CancellationToken cancellationToken) =>
        await context.SaveChangesAsync(cancellationToken);
}
