using Portfolio.Application.About;
using Portfolio.Application.Common.Exceptions;

namespace Portfolio.Infrastructure.Availability;

internal sealed class DatabaseUnavailableAboutRepository : IAboutRepository
{
    public Task<AboutPublicProjection?> GetPublicAsync(string slug, string locale, CancellationToken token) => Fail<AboutPublicProjection?>();
    public Task<Portfolio.Application.Common.Models.About?> GetAdminAsync(CancellationToken token) => Fail<Portfolio.Application.Common.Models.About?>();
    public Task<Portfolio.Application.Common.Models.About?> GetForUpdateAsync(CancellationToken token) => Fail<Portfolio.Application.Common.Models.About?>();
    public Task<Guid?> GetProfileIdAsync(CancellationToken token) => Fail<Guid?>();
    public Task AddAsync(Portfolio.Application.Common.Models.About about, CancellationToken token) => Fail();
    public Task SaveChangesAsync(CancellationToken token) => Fail();
    private static Task<T> Fail<T>() => Task.FromException<T>(Unavailable());
    private static Task Fail() => Task.FromException(Unavailable());
    private static ServiceUnavailableException Unavailable() => new("Database is temporarily unavailable.");
}
