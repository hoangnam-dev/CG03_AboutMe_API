using Portfolio.Infrastructure.Storage;
using Xunit;

namespace Portfolio.IntegrationTests.Storage;

public sealed class SupabaseStorageOptionsValidatorTests
{
    [Fact]
    public void ValidateAcceptsCompleteSafeConfiguration()
    {
        var result = new SupabaseStorageOptionsValidator().Validate(
            null,
            ValidOptions());

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void ValidateRejectsMissingUrlAndServiceKey()
    {
        var result = new SupabaseStorageOptionsValidator().Validate(
            null,
            new SupabaseStorageOptions());

        Assert.True(result.Failed);
        Assert.Contains("Url", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("ServiceRoleKey", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 4096, "RequestTimeoutSeconds")]
    [InlineData(121, 4096, "RequestTimeoutSeconds")]
    [InlineData(30, 100, "MaxResponseBytes")]
    [InlineData(30, 2_000_000, "MaxResponseBytes")]
    public void ValidateRejectsUnsafeTransportBounds(
        int timeoutSeconds,
        int maxResponseBytes,
        string expectedOption)
    {
        var options = ValidOptions();
        options.RequestTimeoutSeconds = timeoutSeconds;
        options.MaxResponseBytes = maxResponseBytes;

        var result = new SupabaseStorageOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains(expectedOption, result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRejectsUnsafeBucketName()
    {
        var options = ValidOptions();
        options.Buckets.Avatars = "../avatars";

        var result = new SupabaseStorageOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Buckets:Avatars", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void ValidateRejectsBucketReuseAcrossDifferentVisibilityPurposes()
    {
        var options = ValidOptions();
        options.Buckets.CertificateFiles = options.Buckets.Avatars;

        var result = new SupabaseStorageOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("must be distinct", result.FailureMessage, StringComparison.Ordinal);
    }

    private static SupabaseStorageOptions ValidOptions() => new()
    {
        Url = new Uri("https://project.supabase.co/"),
        ServiceRoleKey = "server-only-secret",
        RequestTimeoutSeconds = 30,
        MaxResponseBytes = 65_536,
    };
}
