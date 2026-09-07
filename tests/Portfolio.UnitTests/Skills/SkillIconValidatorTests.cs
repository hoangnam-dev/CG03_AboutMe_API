using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Skills;
using Xunit;

namespace Portfolio.UnitTests.Skills;

public sealed class SkillIconValidatorTests
{
    private static readonly SkillIconSettings Settings = new(
        "skill-icons",
        512 * 1024,
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cdn.example.com" });

    [Fact]
    public void RejectsTextLongerThanSixCharacters()
    {
        var error = Assert.Throws<ValidationException>(() =>
            SkillIconValidator.Validate(new SkillIconRequest("text", "TOOLONG"), Settings));

        Assert.Contains("six", error.Errors["icon.value"][0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RejectsLucideNameOutsideAllowlist()
    {
        Assert.Throws<ValidationException>(() =>
            SkillIconValidator.Validate(new SkillIconRequest("lucide", "not-a-real-icon"), Settings));
    }

    [Theory]
    [InlineData("http://cdn.example.com/icon.png")]
    [InlineData("https://evil.example/icon.png")]
    [InlineData("https://cdn.example.com/icon.html")]
    [InlineData("<svg onload=alert(1)>")]
    [InlineData("skills/other/icon.png")]
    public void RejectsUnmanagedImageUrl(string value)
    {
        Assert.Throws<ValidationException>(() =>
            SkillIconValidator.Validate(new SkillIconRequest("image", value), Settings));
    }

    [Theory]
    [InlineData("database-zap")]
    [InlineData("atom")]
    public void AcceptsApprovedLucideNames(string value)
    {
        var result = SkillIconValidator.Validate(new SkillIconRequest("lucide", value), Settings);

        Assert.Equal(value, result.Value);
        Assert.Equal("lucide", result.Type);
    }

    [Fact]
    public void AcceptsConfiguredHttpsImageHost()
    {
        var result = SkillIconValidator.Validate(
            new SkillIconRequest("image", "https://cdn.example.com/icons/dotnet.svg"), Settings);

        Assert.Equal("https://cdn.example.com/icons/dotnet.svg", result.Value);
    }

    [Fact]
    public void RejectsManagedLookingPathOnUntrustedHost()
    {
        Assert.Throws<ValidationException>(() => SkillIconValidator.Validate(
            new SkillIconRequest(
                "image",
                "https://evil.example/storage/v1/object/public/skill-icons/skills/id/icon.svg"),
            Settings));
    }
}
