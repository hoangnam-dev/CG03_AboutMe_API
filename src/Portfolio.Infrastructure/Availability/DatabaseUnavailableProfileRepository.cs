using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Profiles;

namespace Portfolio.Infrastructure.Availability;

internal sealed class DatabaseUnavailableProfileRepository : IProfileRepository
{
    public Task<ProfilePublicProjection?> GetPublicAsync(string slug, string locale, CancellationToken token) => Fail<ProfilePublicProjection?>();
    public Task<Profile?> GetAdminAsync(CancellationToken token) => Fail<Profile?>();
    public Task<Profile?> GetForUpdateAsync(CancellationToken token) => Fail<Profile?>();
    public Task<bool> SlugExistsAsync(string slug, Guid? excludedId, CancellationToken token) => Fail<bool>();
    public Task AddAsync(Profile profile, CancellationToken token) => Fail();
    public Task SaveChangesAsync(CancellationToken token) => Fail();
    private static Task<T> Fail<T>() => Task.FromException<T>(Unavailable());
    private static Task Fail() => Task.FromException(Unavailable());
    private static ServiceUnavailableException Unavailable() => new("Database is temporarily unavailable.");
}
