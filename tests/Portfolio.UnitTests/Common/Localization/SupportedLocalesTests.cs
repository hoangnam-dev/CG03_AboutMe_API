using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Localization;
using Xunit;

namespace Portfolio.UnitTests.Common.Localization;

public sealed class SupportedLocalesTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeReturnsEnglishForMissingLocale(string? locale)
    {
        var result = SupportedLocales.Normalize(locale);

        Assert.Equal("en", result);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("vi")]
    public void NormalizeReturnsSupportedLocale(string locale)
    {
        var result = SupportedLocales.Normalize(locale);

        Assert.Equal(locale, result);
    }

    [Theory]
    [InlineData("EN")]
    [InlineData("fr")]
    [InlineData("en-US")]
    [InlineData(" en ")]
    public void NormalizeRejectsUnsupportedLocale(string locale)
    {
        var exception = Assert.Throws<ValidationException>(
            () => SupportedLocales.Normalize(locale));

        Assert.Equal(["Locale must be either 'en' or 'vi'."], exception.Errors["locale"]);
    }
}
