using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Validation;
using Xunit;

namespace Portfolio.UnitTests.Common.Validation;

public sealed class SlugNormalizerTests
{
    [Theory]
    [InlineData("ASP.NET Core & PostgreSQL", "asp-net-core-postgresql")]
    [InlineData("  multiple---separators  ", "multiple-separators")]
    [InlineData("Đặng Văn Lâm", "dang-van-lam")]
    public void NormalizeProducesLowercaseHyphenatedSlug(string value, string expected)
    {
        var result = SlugNormalizer.Normalize(value);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("***")]
    public void NormalizeRejectsValuesWithoutSlugCharacters(string value)
    {
        var exception = Assert.Throws<ValidationException>(
            () => SlugNormalizer.Normalize(value));

        Assert.Contains("slug", exception.Errors.Keys);
    }
}
