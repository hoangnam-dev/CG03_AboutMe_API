using Portfolio.Api.Configuration;
using Xunit;

namespace Portfolio.IntegrationTests.Api;

public sealed class UploadOptionsValidatorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(10_485_760)]
    [InlineData(26_214_400)]
    public void ValidateAcceptsConfiguredLimitWithinBounds(long maxFileSize)
    {
        var result = new UploadOptionsValidator().Validate(
            null,
            new UploadOptions { MaxFileSize = maxFileSize });

        Assert.True(result.Succeeded);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(26_214_401)]
    public void ValidateRejectsUnsafeConfiguredLimit(long maxFileSize)
    {
        var result = new UploadOptionsValidator().Validate(
            null,
            new UploadOptions { MaxFileSize = maxFileSize });

        Assert.True(result.Failed);
        Assert.Contains("Upload:MaxFileSize", result.FailureMessage, StringComparison.Ordinal);
    }
}
