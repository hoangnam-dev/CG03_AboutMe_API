using Portfolio.Application.Authentication;
using Portfolio.Application.Common.Authentication;
using Portfolio.Application.Common.Exceptions;
using Xunit;

namespace Portfolio.UnitTests.Authentication;

public sealed class AuthServiceTests
{
    [Fact]
    public async Task LoginReturnsIssuedBearerTokenForValidCredentials()
    {
        var user = new AuthenticatedUser(
            Guid.Parse("b3ff6757-af07-45e0-a2b2-4125af88f8ca"),
            "admin@example.com",
            ["Admin"]);
        var expiresAt = new DateTimeOffset(2026, 9, 5, 13, 0, 0, TimeSpan.Zero);
        var service = new AuthService(
            new StubIdentityAuthenticator(user),
            new StubAccessTokenIssuer(new AccessToken("signed-token", expiresAt)));

        var result = await service.LoginAsync(
            new LoginRequest("admin@example.com", "correct-password"),
            TestContext.Current.CancellationToken);

        Assert.Equal("signed-token", result.AccessToken);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal(expiresAt, result.ExpiresAt);
    }

    [Fact]
    public async Task LoginThrowsAuthenticationFailedExceptionForInvalidCredentials()
    {
        var service = new AuthService(
            new StubIdentityAuthenticator(null),
            new StubAccessTokenIssuer(
                new AccessToken("unused", DateTimeOffset.MaxValue)));

        var exception = await Assert.ThrowsAsync<AuthenticationFailedException>(
            () => service.LoginAsync(
                new LoginRequest("unknown@example.com", "wrong-password"),
                TestContext.Current.CancellationToken));

        Assert.Equal("Invalid email or password.", exception.Message);
    }

    private sealed class StubIdentityAuthenticator(AuthenticatedUser? user)
        : IIdentityAuthenticator
    {
        public Task<AuthenticatedUser?> AuthenticateAsync(
            string email,
            string password,
            CancellationToken cancellationToken) => Task.FromResult(user);
    }

    private sealed class StubAccessTokenIssuer(AccessToken token)
        : IAccessTokenIssuer
    {
        public AccessToken Issue(AuthenticatedUser user) => token;
    }
}
