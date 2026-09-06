using Portfolio.Application.Common.Exceptions;

namespace Portfolio.Application.Common.Localization;

public static class SupportedLocales
{
    public const string English = "en";
    public const string Vietnamese = "vi";

    public static IReadOnlySet<string> All { get; } =
        new HashSet<string>([English, Vietnamese], StringComparer.Ordinal);

    public static string Normalize(string? locale)
    {
        if (string.IsNullOrWhiteSpace(locale))
        {
            return English;
        }

        if (All.Contains(locale))
        {
            return locale;
        }

        throw new ValidationException(
            "The requested locale is not supported.",
            new Dictionary<string, string[]>
            {
                ["locale"] = ["Locale must be either 'en' or 'vi'."],
            });
    }
}
