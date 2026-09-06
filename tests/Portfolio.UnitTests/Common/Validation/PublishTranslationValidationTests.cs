using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Validation;
using Xunit;

namespace Portfolio.UnitTests.Common.Validation;

public sealed class PublishTranslationValidationTests
{
    [Fact]
    public void EnsureCompleteAcceptsEnglishAndVietnamese()
    {
        PublishTranslationValidation.EnsureComplete(["vi", "en"]);
    }

    [Theory]
    [MemberData(nameof(IncompleteLocaleSets))]
    public void EnsureCompleteRejectsMissingRequiredTranslation(string[] locales)
    {
        var exception = Assert.Throws<ValidationException>(
            () => PublishTranslationValidation.EnsureComplete(locales));

        Assert.Equal(
            ["Publishing requires both 'en' and 'vi' translations."],
            exception.Errors["translations"]);
    }

    public static TheoryData<string[]> IncompleteLocaleSets
    {
        get
        {
            var data = new TheoryData<string[]>();
            data.Add([]);
            data.Add(["en"]);
            data.Add(["vi"]);
            data.Add(["en", "fr"]);
            return data;
        }
    }
}
