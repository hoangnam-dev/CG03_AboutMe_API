using Microsoft.Extensions.Logging.Abstractions;
using Portfolio.Application.Authentication;
using Portfolio.Application.Common.Authentication;
using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Models;
using Xunit;

namespace Portfolio.UnitTests.Authentication;

public sealed class AuthServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid UserId = Guid.Parse("b3ff6757-af07-45e0-a2b2-4125af88f8ca");
    private static readonly Guid SessionId = Guid.Parse("60ff73e1-683f-436c-86f9-24ae4cc8327f");

    [Fact]
    public async Task LoginCreatesSessionAndReturnsAccessRefreshAndCsrfMaterial()
    {
        var user = new AuthenticatedUser(UserId, "admin@example.com", ["Admin"], 2);
        var repository = new StubSessionRepository();
        var service = CreateService(repository, user);

        var result = await service.LoginAsync(
            new LoginRequest("admin@example.com", "correct-password"),
            new AuthClientContext("203.0.113.10", "test-agent"),
            TestContext.Current.CancellationToken);

        Assert.Equal("signed-access-token", result.Response.AccessToken);
        Assert.Equal("csrf-value", result.Response.CsrfToken);
        Assert.Equal("refresh-value", result.RefreshToken);
        Assert.NotNull(repository.CreatedSession);
        Assert.Equal(UserId, repository.CreatedSession.UserId);
        Assert.NotNull(repository.CreatedToken);
        Assert.Equal(repository.CreatedSession.Id, repository.CreatedToken.SessionId);
    }

    [Fact]
    public async Task LoginUsesGenericFailureWhenCredentialsAreInvalid()
    {
        var service = CreateService(new StubSessionRepository(), null);

        var exception = await Assert.ThrowsAsync<AuthenticationFailedException>(() =>
            service.LoginAsync(
                new LoginRequest("unknown@example.com", "wrong-password"),
                new AuthClientContext("203.0.113.10", null),
                TestContext.Current.CancellationToken));

        Assert.Equal("Invalid email or password.", exception.Message);
    }

    [Fact]
    public async Task RefreshRotatesTokenAndReturnsNewAccessToken()
    {
        var user = new AuthenticatedUser(UserId, "admin@example.com", ["Admin"], 2);
        var repository = new StubSessionRepository
        {
            RotationResult = new RefreshRotationResult(
                RefreshRotationStatus.Succeeded,
                SessionId,
                user,
                Now.AddDays(7)),
        };
        var service = CreateService(repository, user);

        var result = await service.RefreshAsync(
            "old-refresh",
            "csrf-value",
            new AuthClientContext("203.0.113.11", "test-agent"),
            TestContext.Current.CancellationToken);

        Assert.Equal("replacement-refresh", result.RefreshToken);
        Assert.Equal("signed-access-token", result.Response.AccessToken);
        Assert.NotNull(repository.RotationCommand);
    }

    [Fact]
    public async Task RefreshRejectsCsrfTokenNotBoundToSelectedSession()
    {
        var service = CreateService(new StubSessionRepository(), null);

        await Assert.ThrowsAsync<ForbiddenException>(() => service.RefreshAsync(
            "old-refresh",
            "wrong-csrf",
            new AuthClientContext("203.0.113.11", null),
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(RefreshRotationStatus.Invalid)]
    [InlineData(RefreshRotationStatus.Reused)]
    public async Task RefreshUsesGenericFailureForInvalidOrReusedToken(RefreshRotationStatus status)
    {
        var repository = new StubSessionRepository
        {
            RotationResult = new RefreshRotationResult(status, SessionId, null, default),
        };
        var service = CreateService(repository, null);

        await Assert.ThrowsAsync<AuthenticationFailedException>(() => service.RefreshAsync(
            "old-refresh",
            "csrf-value",
            new AuthClientContext("203.0.113.11", null),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RevokeSessionScopesMutationToCurrentUser()
    {
        var repository = new StubSessionRepository();
        var service = CreateService(repository, null);
        var target = Guid.NewGuid();

        await service.RevokeSessionAsync(target, "csrf-value", TestContext.Current.CancellationToken);

        Assert.Equal((UserId, target), repository.RevokedSession);
    }

    [Fact]
    public async Task LogoutRejectsCsrfTokenNotBoundToCurrentSession()
    {
        var service = CreateService(new StubSessionRepository(), null);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.LogoutAsync("wrong-csrf", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SessionsMarksOnlyTheCurrentSession()
    {
        var repository = new StubSessionRepository
        {
            Sessions =
            [
                new AuthSession { Id = SessionId, UserId = UserId },
                new AuthSession { Id = Guid.NewGuid(), UserId = UserId },
            ],
        };
        var service = CreateService(repository, null);

        var sessions = await service.GetSessionsAsync(TestContext.Current.CancellationToken);

        Assert.Single(sessions, session => session.IsCurrent);
        Assert.Equal(SessionId, sessions.Single(session => session.IsCurrent).Id);
    }

    private static AuthService CreateService(StubSessionRepository repository, AuthenticatedUser? user) =>
        new(
            new StubIdentityAuthenticator(user),
            new StubAccessTokenIssuer(),
            repository,
            new StubRefreshTokenProtector(),
            new StubCsrfTokenService(),
            new StubSessionLifetime(),
            new StubCurrentUserAccessor(),
            new StubTimeProvider(),
            NullLogger<AuthService>.Instance);

    private sealed class StubIdentityAuthenticator(AuthenticatedUser? user) : IIdentityAuthenticator
    {
        public Task<AuthenticatedUser?> AuthenticateAsync(string email, string password, CancellationToken cancellationToken) =>
            Task.FromResult(user);
    }

    private sealed class StubAccessTokenIssuer : IAccessTokenIssuer
    {
        public AccessToken Issue(AuthenticatedUser user, Guid sessionId) =>
            new("signed-access-token", Now.AddMinutes(10));
    }

    private sealed class StubRefreshTokenProtector : IRefreshTokenProtector
    {
        private int _count;

        public GeneratedRefreshToken Generate(Guid tokenId)
        {
            _count++;
            return new GeneratedRefreshToken(
                tokenId,
                _count == 1 ? "refresh-value" : "replacement-refresh",
                [_count == 1 ? (byte)1 : (byte)2]);
        }

        public ParsedRefreshToken? ParseAndHash(string value)
        {
            if (value != "old-refresh")
            {
                return null;
            }

            _count = 1;
            return new ParsedRefreshToken(Guid.NewGuid(), [9]);
        }

        public bool FixedTimeEquals(ReadOnlySpan<byte> storedHash, ReadOnlySpan<byte> candidateHash) =>
            storedHash.SequenceEqual(candidateHash);
    }

    private sealed class StubCsrfTokenService : ICsrfTokenService
    {
        public string Issue(Guid sessionId) => "csrf-value";
        public bool Validate(Guid sessionId, string token) => token == "csrf-value";
    }

    private sealed class StubSessionLifetime : IAuthSessionLifetime
    {
        public TimeSpan IdleLifetime => TimeSpan.FromDays(7);
        public TimeSpan AbsoluteLifetime => TimeSpan.FromDays(30);
    }

    private sealed class StubCurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentAuthSession GetRequired() => new(UserId, SessionId, 2);
    }

    private sealed class StubTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class StubSessionRepository : IAuthSessionRepository
    {
        public AuthSession? CreatedSession { get; private set; }
        public RefreshToken? CreatedToken { get; private set; }
        public RefreshRotationCommand? RotationCommand { get; private set; }
        public RefreshRotationResult RotationResult { get; set; } =
            new(RefreshRotationStatus.Invalid, Guid.Empty, null, default);
        public (Guid UserId, Guid SessionId)? RevokedSession { get; private set; }
        public IReadOnlyList<AuthSession> Sessions { get; set; } = [];

        public Task CreateAsync(AuthSession session, RefreshToken refreshToken, CancellationToken cancellationToken)
        {
            CreatedSession = session;
            CreatedToken = refreshToken;
            return Task.CompletedTask;
        }

        public Task<Guid?> GetSessionIdForTokenAsync(Guid tokenId, CancellationToken cancellationToken) =>
            Task.FromResult<Guid?>(SessionId);

        public Task<RefreshRotationResult> RotateAsync(RefreshRotationCommand command, CancellationToken cancellationToken)
        {
            RotationCommand = command;
            return Task.FromResult(RotationResult);
        }

        public Task RevokeAsync(Guid userId, Guid sessionId, DateTimeOffset revokedAt, string reason, CancellationToken cancellationToken)
        {
            RevokedSession = (userId, sessionId);
            return Task.CompletedTask;
        }

        public Task RevokeAllAsync(Guid userId, DateTimeOffset revokedAt, string reason, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<AuthSession>> GetByUserAsync(
            Guid userId,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            Task.FromResult(Sessions);
    }
}
