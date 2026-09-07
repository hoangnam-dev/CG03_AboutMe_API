using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Application.About;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Models;
using Xunit;

namespace Portfolio.UnitTests.About;

public sealed class AboutServiceTests
{
    [Fact]
    public async Task GetPublicDoesNotReturnDraft()
    {
        var service = new AboutService(new AboutRepositoryStub(), TimeProvider.System,
            NullLogger<AboutService>.Instance);

        await Assert.ThrowsAsync<NotFoundException>(() => service.GetPublicAsync(
            "nam", "en", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateRejectsNegativeCounters()
    {
        var service = new AboutService(new AboutRepositoryStub { ProfileId = Guid.NewGuid() },
            TimeProvider.System, NullLogger<AboutService>.Instance);
        var request = new AboutUpdateRequest(-1, 1, 1, true, true, true, true, false,
            new Dictionary<string, AboutTranslationRequest>
            {
                ["en"] = new("About", null), ["vi"] = new("Gioi thieu", null),
            });

        var error = await Assert.ThrowsAsync<ValidationException>(() => service.UpdateAsync(
            request, TestContext.Current.CancellationToken));

        Assert.Contains("non-negative", error.Errors["counters"][0], StringComparison.OrdinalIgnoreCase);
    }

    private sealed class AboutRepositoryStub : IAboutRepository
    {
        public Guid? ProfileId { get; init; }
        public Task<AboutPublicProjection?> GetPublicAsync(string slug, string locale, CancellationToken token) =>
            Task.FromResult<AboutPublicProjection?>(null);
        public Task<Portfolio.Application.Common.Models.About?> GetAdminAsync(CancellationToken token) => Task.FromResult<Portfolio.Application.Common.Models.About?>(null);
        public Task<Portfolio.Application.Common.Models.About?> GetForUpdateAsync(CancellationToken token) => Task.FromResult<Portfolio.Application.Common.Models.About?>(null);
        public Task<Guid?> GetProfileIdAsync(CancellationToken token) => Task.FromResult(ProfileId);
        public Task AddAsync(Portfolio.Application.Common.Models.About about, CancellationToken token) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken token) => Task.CompletedTask;
    }
}
