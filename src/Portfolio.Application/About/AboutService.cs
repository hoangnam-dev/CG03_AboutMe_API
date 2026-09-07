using Microsoft.Extensions.Logging;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Localization;
using Portfolio.Application.Common.Models;
using Portfolio.Application.Common.Validation;

namespace Portfolio.Application.About;

public sealed partial class AboutService(
    IAboutRepository repository,
    TimeProvider timeProvider,
    ILogger<AboutService> logger) : IAboutService
{
    public async Task<PublicAboutResponse> GetPublicAsync(
        string slug, string? locale, CancellationToken cancellationToken)
    {
        var projection = await repository.GetPublicAsync(
            SlugNormalizer.Normalize(slug), SupportedLocales.Normalize(locale), cancellationToken)
            ?? throw new NotFoundException("Published About content was not found.");
        return new PublicAboutResponse(
            projection.Content, projection.CareerGoal,
            projection.ShowYearsOfExperience ? projection.YearsOfExperience : null,
            projection.ShowProjectCount ? projection.ProjectCount : null,
            projection.ShowTechnologyCount ? projection.TechnologyCount : null,
            projection.ShowYearsOfExperience, projection.ShowProjectCount,
            projection.ShowTechnologyCount, projection.ShowContactSection);
    }

    public async Task<AboutAdminResponse> GetAdminAsync(CancellationToken cancellationToken) =>
        Map(await repository.GetAdminAsync(cancellationToken)
            ?? throw new NotFoundException("About content was not found."));

    public async Task<AboutAdminResponse> UpdateAsync(
        AboutUpdateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.YearsOfExperience < 0 || request.ProjectCount < 0 || request.TechnologyCount < 0)
            throw Invalid("counters", "About counters must be non-negative.");
        if (request.Translations.Keys.Any(locale => !SupportedLocales.All.Contains(locale)))
            throw Invalid("translations", "Only 'en' and 'vi' translations are supported.");
        PublishTranslationValidation.EnsureComplete(request.Translations.Keys);
        if (request.IsPublished)
        {
            PublishTranslationValidation.EnsureComplete(request.Translations
                .Where(pair => !string.IsNullOrWhiteSpace(pair.Value.Content))
                .Select(pair => pair.Key));
        }

        var about = await repository.GetForUpdateAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        if (about is null)
        {
            var profileId = await repository.GetProfileIdAsync(cancellationToken)
                ?? throw new NotFoundException("Profile must exist before About content can be created.");
            about = new Common.Models.About { Id = Guid.NewGuid(), ProfileId = profileId, CreatedAt = now };
            await repository.AddAsync(about, cancellationToken);
        }

        about.YearsOfExperience = request.YearsOfExperience;
        about.ProjectCount = request.ProjectCount;
        about.TechnologyCount = request.TechnologyCount;
        about.ShowYearsOfExperience = request.ShowYearsOfExperience;
        about.ShowProjectCount = request.ShowProjectCount;
        about.ShowTechnologyCount = request.ShowTechnologyCount;
        about.ShowContactSection = request.ShowContactSection;
        about.IsPublished = request.IsPublished;
        about.UpdatedAt = now;
        foreach (var translation in request.Translations.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var target = about.Translations.SingleOrDefault(
                current => current.LocaleCode == translation.Key);
            if (target is null)
            {
                target = new AboutTranslation { AboutId = about.Id, LocaleCode = translation.Key };
                about.Translations.Add(target);
            }
            target.Content = NullIfWhiteSpace(translation.Value.Content);
            target.CareerGoal = NullIfWhiteSpace(translation.Value.CareerGoal);
        }

        await repository.SaveChangesAsync(cancellationToken);
        LogAboutUpdated(logger, about.Id);
        return Map(about);
    }

    private static AboutAdminResponse Map(Common.Models.About about) => new(
        about.Id, about.YearsOfExperience, about.ProjectCount, about.TechnologyCount,
        about.ShowYearsOfExperience, about.ShowProjectCount, about.ShowTechnologyCount,
        about.ShowContactSection, about.IsPublished,
        about.Translations.ToDictionary(
            translation => translation.LocaleCode,
            translation => new AboutTranslationRequest(translation.Content, translation.CareerGoal),
            StringComparer.Ordinal));

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ValidationException Invalid(string field, string message) =>
        new("About validation failed.", new Dictionary<string, string[]> { [field] = [message] });

    [LoggerMessage(EventId = 3101, Level = LogLevel.Information, Message = "About {AboutId} updated.")]
    private static partial void LogAboutUpdated(ILogger logger, Guid aboutId);
}
