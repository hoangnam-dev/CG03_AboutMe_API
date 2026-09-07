using Microsoft.Extensions.Options;
using Portfolio.Infrastructure.Authentication;
using Xunit;

namespace Portfolio.IntegrationTests.Authentication;

public sealed class RefreshTokenProtectorTests
{
    private static readonly RefreshTokenOptions OptionsValue = new()
    {
        Pepper = "integration-test-pepper-with-at-least-32-bytes",
    };

    [Fact]
    public void GenerateCreatesSelectorAndAtLeast256BitOpaqueSecret()
    {
        var protector = new RefreshTokenProtector(Options.Create(OptionsValue));
        var tokenId = Guid.Parse("708030fb-8ce5-4092-b8c6-065899c13e42");

        var generated = protector.Generate(tokenId);
        var parts = generated.Value.Split('.');

        Assert.Equal(2, parts.Length);
        Assert.Equal(tokenId, Guid.Parse(parts[0]));
        Assert.True(Microsoft.IdentityModel.Tokens.Base64UrlEncoder.DecodeBytes(parts[1]).Length >= 32);
        Assert.DoesNotContain(parts[1], Convert.ToHexString(generated.SecretHash), StringComparison.Ordinal);
    }

    [Fact]
    public void ParseAndHashVerifiesOnlyTheOriginalSecret()
    {
        var protector = new RefreshTokenProtector(Options.Create(OptionsValue));
        var generated = protector.Generate(Guid.NewGuid());

        var parsed = protector.ParseAndHash(generated.Value);
        var wrongSecret = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(new byte[32]);
        var wrong = protector.ParseAndHash($"{generated.TokenId:D}.{wrongSecret}");

        Assert.NotNull(parsed);
        Assert.Equal(generated.TokenId, parsed.TokenId);
        Assert.True(protector.FixedTimeEquals(generated.SecretHash, parsed.SecretHash));
        Assert.NotNull(wrong);
        Assert.False(protector.FixedTimeEquals(generated.SecretHash, wrong.SecretHash));
        Assert.Null(protector.ParseAndHash("not-a-refresh-token"));
        var nonCanonicalSecret = Microsoft.IdentityModel.Tokens.Base64UrlEncoder.Encode(new byte[33]);
        Assert.Null(protector.ParseAndHash($"{generated.TokenId:D}.{nonCanonicalSecret}"));
    }

    [Fact]
    public void SessionCsrfTokenIsBoundToOneSession()
    {
        var service = new SessionCsrfTokenService(Options.Create(OptionsValue));
        var sessionId = Guid.NewGuid();
        var token = service.Issue(sessionId);

        Assert.True(service.Validate(sessionId, token));
        Assert.False(service.Validate(Guid.NewGuid(), token));
        Assert.False(service.Validate(sessionId, token + "tampered"));
    }
}
