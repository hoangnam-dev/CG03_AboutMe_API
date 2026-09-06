using System.Globalization;
using System.Text;
using Portfolio.Application.Common.Exceptions;

namespace Portfolio.Application.Common.Validation;

public static class SlugNormalizer
{
    public static string Normalize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var decomposed = value.Replace('Đ', 'D').Replace('đ', 'd')
            .Normalize(NormalizationForm.FormD);
        var slug = new StringBuilder(decomposed.Length);
        var separatorPending = false;

        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsAsciiLetterOrDigit(character))
            {
                if (separatorPending && slug.Length > 0)
                {
                    slug.Append('-');
                }

                slug.Append(char.ToLowerInvariant(character));
                separatorPending = false;
            }
            else if (slug.Length > 0)
            {
                separatorPending = true;
            }
        }

        if (slug.Length == 0)
        {
            throw new ValidationException(
                "A slug could not be generated from the supplied value.",
                new Dictionary<string, string[]>
                {
                    ["slug"] = ["Slug must contain at least one ASCII letter or digit."],
                });
        }

        return slug.ToString();
    }
}
