using Portfolio.Infrastructure.Authentication;
using Xunit;

namespace Portfolio.IntegrationTests.Authentication;

public sealed class BootstrapAdminOptionsValidatorTests
{
    [Fact]
    public void ValidateSucceedsWhenBootstrapIsDisabledWithoutCredentials()
    {
        var result = new BootstrapAdminOptionsValidator().Validate(
            null,
            new BootstrapAdminOptions { Enabled = false });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ValidateFailsWhenBootstrapIsEnabledWithoutCredentials()
    {
        var result = new BootstrapAdminOptionsValidator().Validate(
            null,
            new BootstrapAdminOptions { Enabled = true });

        Assert.True(result.Failed);
        Assert.Contains("Email", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("Password", result.FailureMessage, StringComparison.Ordinal);
    }
}
