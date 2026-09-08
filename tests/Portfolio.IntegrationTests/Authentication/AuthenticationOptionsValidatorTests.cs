using Portfolio.Api.Configuration;
using Portfolio.Infrastructure.Authentication;
using Xunit;

namespace Portfolio.IntegrationTests.Authentication;

public sealed class AuthenticationOptionsValidatorTests
{
    [Fact]
    public void FrontendValidationAcceptsMultipleExactOrigins()
    {
        var result = new FrontendOptionsValidator().Validate(null, new FrontendOptions
        {
            Origins =
            [
                "https://localhost:44313",
                "https://localhost:7097",
                "http://localhost:3000",
            ],
        });

        Assert.True(result.Succeeded, result.FailureMessage);
    }

    [Fact]
    public void FrontendValidationRejectsEmptyAllowlist()
    {
        var result = new FrontendOptionsValidator().Validate(null, new FrontendOptions());

        Assert.True(result.Failed);
        Assert.Contains("at least one", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("not-an-origin")]
    [InlineData("ftp://localhost:3000")]
    [InlineData("https://localhost:44313/swagger")]
    [InlineData("https://user@localhost:44313")]
    public void FrontendValidationRejectsInvalidOrigin(string origin)
    {
        var result = new FrontendOptionsValidator().Validate(null, new FrontendOptions
        {
            Origins = [origin],
        });

        Assert.True(result.Failed);
    }

    [Fact]
    public void JwtDefaultsUseShortLivedAccessTokensAndBoundedClockSkew()
    {
        var options = new JwtOptions();

        Assert.Equal(10, options.AccessTokenMinutes);
        Assert.Equal(30, options.ClockSkewSeconds);
    }

    [Fact]
    public void JwtValidationRejectsMissingAsymmetricSigningConfiguration()
    {
        var result = new JwtOptionsValidator().Validate(null, new JwtOptions
        {
            Issuer = "portfolio-tests",
            Audience = "portfolio-client",
        });

        Assert.True(result.Failed);
        Assert.Contains("ActiveKeyId", result.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("SigningCertificatePath", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(0, 30)]
    [InlineData(61, 30)]
    [InlineData(10, -1)]
    [InlineData(10, 61)]
    public void JwtValidationRejectsUnsafeLifetimeOrClockSkew(int minutes, int skewSeconds)
    {
        var result = new JwtOptionsValidator().Validate(null, new JwtOptions
        {
            Issuer = "portfolio-tests",
            Audience = "portfolio-client",
            ActiveKeyId = "test-key",
            SigningCertificatePath = "missing-test-certificate.pfx",
            AccessTokenMinutes = minutes,
            ClockSkewSeconds = skewSeconds,
        });

        Assert.True(result.Failed);
    }

    [Fact]
    public void RefreshTokenDefaultsMatchSessionPolicy()
    {
        var options = new RefreshTokenOptions();

        Assert.Equal(7, options.IdleLifetimeDays);
        Assert.Equal(30, options.AbsoluteLifetimeDays);
        Assert.Equal("__Host-refresh", options.CookieName);
        Assert.Equal("__Host-csrf", options.CsrfCookieName);
        Assert.Equal("Lax", options.CookieSameSite);
    }

    [Theory]
    [InlineData(0, 30, "abcdefghijklmnopqrstuvwxyz123456")]
    [InlineData(31, 30, "abcdefghijklmnopqrstuvwxyz123456")]
    [InlineData(7, 30, "too-short")]
    [InlineData(7, 30, "abcdefghijklmnopqrstuvwxyz123456")]
    public void RefreshTokenValidationRejectsInvalidSettings(
        int idleDays,
        int absoluteDays,
        string pepper)
    {
        var options = new RefreshTokenOptions
        {
            IdleLifetimeDays = idleDays,
            AbsoluteLifetimeDays = absoluteDays,
            Pepper = pepper,
            CookieName = idleDays == 7 ? "refresh" : "__Host-refresh",
            CsrfCookieName = idleDays == 7 ? "csrf" : "__Host-csrf",
            CookieSameSite = idleDays == 7 ? "Invalid" : "Lax",
        };

        var result = new RefreshTokenOptionsValidator().Validate(null, options);

        Assert.True(result.Failed);
    }
}
