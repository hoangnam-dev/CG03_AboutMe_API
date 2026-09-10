using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Portfolio.Application.Common.Authentication;
using Portfolio.Infrastructure.Authentication;
using Xunit;

namespace Portfolio.IntegrationTests.Authentication;

public sealed class JwtAccessTokenIssuerTests : IDisposable
{
    private readonly string _certificatePath;

    public JwtAccessTokenIssuerTests()
    {
        _certificatePath = Path.Combine(Path.GetTempPath(), $"portfolio-jwt-{Guid.NewGuid():N}.pfx");
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=portfolio-tests",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(2));
        File.WriteAllBytes(_certificatePath, certificate.Export(X509ContentType.Pfx, "test-password"));
    }

    [Fact]
    public void IssueCreatesExpectedIdentityAndRoleClaims()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "portfolio-tests",
            Audience = "portfolio-client",
            ActiveKeyId = "test-key-2026-09",
            SigningCertificatePath = _certificatePath,
            SigningCertificatePassword = "test-password",
            AccessTokenMinutes = 10,
            ClockSkewSeconds = 30,
        });
        var clock = new StubTimeProvider(
            new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero));
        using var keyRing = new JwtKeyRing(options);
        var issuer = new JwtAccessTokenIssuer(options, keyRing, clock);
        var sessionId = Guid.Parse("6bc6a62a-7016-43ab-9d7e-acad03697bf7");
        var user = new AuthenticatedUser(
            Guid.Parse("83ed5538-45b1-4500-8ae5-c3e6f71c98fa"),
            "admin@example.com",
            ["Admin"],
            4);

        var result = issuer.Issue(user, sessionId);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Value);

        Assert.Equal("portfolio-tests", token.Issuer);
        Assert.Contains("portfolio-client", token.Audiences);
        Assert.Equal(user.Id.ToString(), token.Subject);
        Assert.Equal(SecurityAlgorithms.RsaSha256, token.Header.Alg);
        Assert.Equal("test-key-2026-09", token.Header.Kid);
        Assert.Equal("JWT", token.Header.Typ);
        Assert.Equal(sessionId.ToString(), token.Claims.Single(x => x.Type == "sid").Value);
        Assert.Equal("4", token.Claims.Single(x => x.Type == "auth_version").Value);
        Assert.NotNull(token.Claims.SingleOrDefault(x => x.Type == JwtRegisteredClaimNames.Jti));
        Assert.NotNull(token.Claims.SingleOrDefault(x => x.Type == JwtRegisteredClaimNames.Iat));
        Assert.NotNull(token.Payload.NotBefore);
        Assert.NotNull(token.Payload.Expiration);
        Assert.Equal("admin@example.com", token.Claims.Single(x => x.Type == ClaimTypes.Email).Value);
        Assert.Equal("Admin", token.Claims.Single(x => x.Type == ClaimTypes.Role).Value);
        Assert.Equal(clock.GetUtcNow().AddMinutes(10), result.ExpiresAt);
    }

    public void Dispose() => File.Delete(_certificatePath);

    private sealed class StubTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
