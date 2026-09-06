using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Localization;

namespace Portfolio.Application.Common.Validation;

public static class PublishTranslationValidation
{
    public static void EnsureComplete(IEnumerable<string> locales)
    {
        ArgumentNullException.ThrowIfNull(locales);

        var provided = locales.ToHashSet(StringComparer.Ordinal);
        if (SupportedLocales.All.IsSubsetOf(provided))
        {
            return;
        }

        throw new ValidationException(
            "Required translations are missing.",
            new Dictionary<string, string[]>
            {
                ["translations"] = ["Publishing requires both 'en' and 'vi' translations."],
            });
    }
}
