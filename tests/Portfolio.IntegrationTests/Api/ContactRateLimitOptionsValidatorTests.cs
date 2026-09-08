using Portfolio.Api.Configuration;
using Xunit;

namespace Portfolio.IntegrationTests.Api;

public sealed class ContactRateLimitOptionsValidatorTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(5, 60)]
    [InlineData(100, 3600)]
    public void ValidateAcceptsBoundedValues(int permitLimit, int windowSeconds)
    {
        var result = new ContactRateLimitOptionsValidator().Validate(
            null,
            new ContactRateLimitOptions
            {
                PermitLimit = permitLimit,
                WindowSeconds = windowSeconds,
            });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0, 60)]
    [InlineData(101, 60)]
    [InlineData(5, 0)]
    [InlineData(5, 3601)]
    public void ValidateRejectsUnsafeValues(int permitLimit, int windowSeconds)
    {
        var result = new ContactRateLimitOptionsValidator().Validate(
            null,
            new ContactRateLimitOptions
            {
                PermitLimit = permitLimit,
                WindowSeconds = windowSeconds,
            });

        Assert.True(result.Failed);
        Assert.Contains("Contact rate limit", result.FailureMessage, StringComparison.Ordinal);
    }
}
