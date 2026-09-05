using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Portfolio.Application.Common.Authentication;
using Portfolio.Infrastructure.Authentication;
using Xunit;

namespace Portfolio.IntegrationTests.Authentication;

public sealed class JwtAccessTokenIssuerTests
{
    [Fact]
    public void IssueCreatesExpectedIdentityAndRoleClaims()
    {
        var options = Options.Create(new JwtOptions
        {
            Issuer = "portfolio-tests",
            Audience = "portfolio-client",
            SigningKey = "this-is-a-strong-test-signing-key-with-more-than-32-bytes",
            AccessTokenMinutes = 60,
        });
        var clock = new StubTimeProvider(
            new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero));
        var issuer = new JwtAccessTokenIssuer(options, clock);
        var user = new AuthenticatedUser(
            Guid.Parse("83ed5538-45b1-4500-8ae5-c3e6f71c98fa"),
            "admin@example.com",
            ["Admin"]);

        var result = issuer.Issue(user);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(result.Value);

        Assert.Equal("portfolio-tests", token.Issuer);
        Assert.Contains("portfolio-client", token.Audiences);
        Assert.Equal(user.Id.ToString(), token.Subject);
        Assert.Equal("admin@example.com", token.Claims.Single(x => x.Type == ClaimTypes.Email).Value);
        Assert.Equal("Admin", token.Claims.Single(x => x.Type == ClaimTypes.Role).Value);
        Assert.Equal(clock.GetUtcNow().AddMinutes(60), result.ExpiresAt);
    }

    private sealed class StubTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
