using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Experiences;
using Xunit;

namespace Portfolio.UnitTests.Experiences;

public sealed class ExperienceServiceTests
{
    [Fact]
    public async Task CreateRejectsEndBeforeStart()
    {
        var service = CreateService(new ExperienceRepositoryStub());
        var request = Request() with { EndDate = new DateOnly(2023, 12, 31) };

        var error = await Assert.ThrowsAsync<ValidationException>(() => service.CreateExperienceAsync(
            request, TestContext.Current.CancellationToken));

        Assert.Contains("on or after", error.Errors["endDate"][0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PublishingRequiresBothTranslations()
    {
        var service = CreateService(new ExperienceRepositoryStub());
        var request = Request() with
        {
            Translations = new Dictionary<string, ExperienceTranslationRequest>
            {
                ["en"] = new("Backend Engineer", "Remote", "Built APIs."),
            },
        };

        await Assert.ThrowsAsync<ValidationException>(() => service.CreateExperienceAsync(
            request, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CreateRejectsUnknownTechnology()
    {
        var repository = new ExperienceRepositoryStub { TechnologiesExist = false };
        var service = CreateService(repository);

        var error = await Assert.ThrowsAsync<ValidationException>(() => service.CreateExperienceAsync(
            Request(), TestContext.Current.CancellationToken));

        Assert.Contains("exist", error.Errors["technologyIds"][0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateRejectsDuplicateHighlightOrderWithinLocaleAndType()
    {
        var service = CreateService(new ExperienceRepositoryStub());
        var request = Request() with
        {
            Highlights =
            [
                new("en", "achievement", "Reduced latency.", 0),
                new("en", "achievement", "Improved throughput.", 0),
            ],
        };

        var error = await Assert.ThrowsAsync<ValidationException>(() => service.CreateExperienceAsync(
            request, TestContext.Current.CancellationToken));

        Assert.Contains("unique", error.Errors["highlights.displayOrder"][0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateDerivesTechnologyDisplayOrderFromRequestOrder()
    {
        var repository = new ExperienceRepositoryStub();
        var service = CreateService(repository);
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        var result = await service.CreateExperienceAsync(
            Request() with { TechnologyIds = [second, first] },
            TestContext.Current.CancellationToken);

        Assert.NotNull(repository.Added);
        Assert.Equal(
            [(second, 0), (first, 1)],
            repository.Added.Technologies
                .OrderBy(link => link.DisplayOrder)
                .Select(link => (link.TechnologyId, link.DisplayOrder)));
        Assert.True(result.IsCurrent);
    }

    [Fact]
    public async Task UpdateReplacesAggregateChildren()
    {
        var experience = new WorkExperience { Id = Guid.NewGuid() };
        experience.Translations.Add(new ExperienceTranslation
        {
            ExperienceId = experience.Id,
            LocaleCode = "en",
            Position = "Old",
        });
        experience.Highlights.Add(new ExperienceHighlight
        {
            Id = Guid.NewGuid(),
            ExperienceId = experience.Id,
            LocaleCode = "en",
            HighlightType = ExperienceHighlightType.Responsibility,
            Content = "Old",
        });
        experience.Technologies.Add(new ExperienceTechnology
        {
            ExperienceId = experience.Id,
            TechnologyId = Guid.NewGuid(),
        });
        var repository = new ExperienceRepositoryStub { Existing = experience };
        var service = CreateService(repository);

        var result = await service.UpdateExperienceAsync(
            experience.Id, Request(), TestContext.Current.CancellationToken);

        Assert.Equal("Backend Engineer", result.Translations["en"].Position);
        Assert.Equal(2, experience.Translations.Count);
        Assert.Equal(2, experience.Highlights.Count);
        Assert.Equal(2, experience.Technologies.Count);
    }

    [Fact]
    public async Task UpdateReusesRetainedHighlightAndTechnologyRows()
    {
        var experienceId = Guid.NewGuid();
        var technologyId = Guid.NewGuid();
        var highlight = new ExperienceHighlight
        {
            Id = Guid.NewGuid(),
            ExperienceId = experienceId,
            LocaleCode = "en",
            HighlightType = ExperienceHighlightType.Achievement,
            Content = "Old content",
            DisplayOrder = 0,
        };
        var technology = new ExperienceTechnology
        {
            ExperienceId = experienceId,
            TechnologyId = technologyId,
            DisplayOrder = 4,
        };
        var experience = new WorkExperience { Id = experienceId };
        experience.Highlights.Add(highlight);
        experience.Technologies.Add(technology);
        var repository = new ExperienceRepositoryStub { Existing = experience };
        var service = CreateService(repository);
        var request = Request() with
        {
            Highlights = [new("en", "achievement", "Updated content", 0)],
            TechnologyIds = [technologyId],
        };

        await service.UpdateExperienceAsync(
            experienceId, request, TestContext.Current.CancellationToken);

        Assert.Same(highlight, Assert.Single(experience.Highlights));
        Assert.Equal("Updated content", highlight.Content);
        Assert.Same(technology, Assert.Single(experience.Technologies));
        Assert.Equal(0, technology.DisplayOrder);
    }

    [Fact]
    public async Task ReorderRejectsDuplicateIds()
    {
        var id = Guid.NewGuid();
        var service = CreateService(new ExperienceRepositoryStub());

        await Assert.ThrowsAsync<ValidationException>(() => service.ReorderExperiencesAsync(
            new ExperienceReorderRequest([new(id, 0), new(id, 1)]),
            TestContext.Current.CancellationToken));
    }

    private static ExperienceService CreateService(IExperienceRepository repository) =>
        new(repository, TimeProvider.System, NullLogger<ExperienceService>.Instance);

    private static ExperienceWriteRequest Request() => new(
        "Example Co",
        "Full-time",
        "https://example.com",
        new DateOnly(2024, 1, 1),
        null,
        0,
        true,
        new Dictionary<string, ExperienceTranslationRequest>
        {
            ["en"] = new("Backend Engineer", "Remote", "Built APIs."),
            ["vi"] = new("Ky su Backend", "Tu xa", "Xay dung API."),
        },
        [
            new("en", "achievement", "Reduced latency.", 0),
            new("vi", "achievement", "Giam do tre.", 0),
        ],
        [Guid.NewGuid(), Guid.NewGuid()]);

    private sealed class ExperienceRepositoryStub : IExperienceRepository
    {
        public bool TechnologiesExist { get; init; } = true;
        public WorkExperience? Existing { get; init; }
        public WorkExperience? Added { get; private set; }

        public Task<IReadOnlyList<PublicExperienceResponse>?> GetPublicAsync(string slug, string locale, CancellationToken token) => throw new NotImplementedException();
        public Task<ExperienceAdminPage> GetExperiencesAsync(ExperienceAdminQuery query, CancellationToken token) => throw new NotImplementedException();
        public Task<WorkExperience?> GetAsync(Guid id, bool tracked, CancellationToken token) => Task.FromResult(Existing?.Id == id ? Existing : null);
        public Task<bool> TechnologyIdsExistAsync(IReadOnlyCollection<Guid> ids, CancellationToken token) => Task.FromResult(TechnologiesExist);
        public Task AddAsync(WorkExperience experience, CancellationToken token)
        {
            Added = experience;
            return Task.CompletedTask;
        }
        public void Remove(WorkExperience experience) { }
        public Task<ExperienceReorderResult> ReorderAsync(IReadOnlyList<ExperienceOrderItem> items, CancellationToken token) => Task.FromResult(ExperienceReorderResult.Success);
        public Task SaveChangesAsync(CancellationToken token) => Task.CompletedTask;
    }
}
