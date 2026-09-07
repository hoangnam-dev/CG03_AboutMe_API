using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Portfolio.Application.Authentication;
using Portfolio.Application.Common.Models;
using Portfolio.Infrastructure.Authentication;
using Portfolio.Infrastructure.Persistence.Repositories;
using Portfolio.IntegrationTests.Infrastructure;
using Xunit;

namespace Portfolio.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class AuthSessionPersistenceTests(PostgreSqlFixture database)
{
    [Fact]
    public async Task DatabaseRejectsTwoActiveRefreshTokensForOneSession()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        await using var context = database.CreateDbContext();
        var userId = await AddUserAsync(context);
        var session = CreateSession(userId);
        session.RefreshTokens.Add(CreateToken(session.Id));
        session.RefreshTokens.Add(CreateToken(session.Id));
        context.AuthSessions.Add(session);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ParallelRefreshAllowsAtMostOneRotationAndRevokesSessionOnReuse()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        var options = Options.Create(new RefreshTokenOptions
        {
            Pepper = "integration-test-pepper-with-at-least-32-bytes",
        });
        var protector = new RefreshTokenProtector(options);
        var generated = protector.Generate(Guid.NewGuid());
        Guid sessionId;
        await using (var setup = database.CreateDbContext())
        {
            var userId = await AddUserAsync(setup);
            var session = CreateSession(userId);
            sessionId = session.Id;
            session.RefreshTokens.Add(CreateToken(session.Id, generated.TokenId, generated.SecretHash));
            setup.AuthSessions.Add(session);
            await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var parsed = protector.ParseAndHash(generated.Value)!;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<RefreshRotationResult> RotateAsync()
        {
            await using var context = database.CreateDbContext();
            var repository = new AuthSessionRepository(context, protector);
            await start.Task;
            var replacement = protector.Generate(Guid.NewGuid());
            return await repository.RotateAsync(
                new RefreshRotationCommand(
                    parsed.TokenId,
                    parsed.SecretHash,
                    replacement.TokenId,
                    replacement.SecretHash,
                    DateTimeOffset.UtcNow,
                    TimeSpan.FromDays(7),
                    "203.0.113.20"),
                TestContext.Current.CancellationToken);
        }

        var first = RotateAsync();
        var second = RotateAsync();
        start.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, result => result.Status == RefreshRotationStatus.Succeeded);
        Assert.Single(results, result => result.Status == RefreshRotationStatus.Reused);
        await using var verification = database.CreateDbContext();
        var storedSession = await verification.AuthSessions
            .Include(session => session.RefreshTokens)
            .SingleAsync(session => session.Id == sessionId, TestContext.Current.CancellationToken);
        Assert.NotNull(storedSession.RevokedAt);
        Assert.All(storedSession.RefreshTokens, token => Assert.NotNull(token.RevokedAt));
    }

    [Theory]
    [InlineData("token-expired")]
    [InlineData("idle-expired")]
    [InlineData("absolute-expired")]
    [InlineData("session-revoked")]
    [InlineData("user-disabled")]
    public async Task RefreshRejectsExpiredRevokedOrDisabledState(string invalidState)
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        var now = DateTimeOffset.UtcNow;
        var options = Options.Create(new RefreshTokenOptions
        {
            Pepper = "integration-test-pepper-with-at-least-32-bytes",
        });
        var protector = new RefreshTokenProtector(options);
        var generated = protector.Generate(Guid.NewGuid());
        await using (var setup = database.CreateDbContext())
        {
            var userId = await AddUserAsync(setup);
            var session = CreateSession(userId);
            var token = CreateToken(session.Id, generated.TokenId, generated.SecretHash);
            session.RefreshTokens.Add(token);
            switch (invalidState)
            {
                case "token-expired":
                    token.ExpiresAt = now.AddMinutes(-1);
                    break;
                case "idle-expired":
                    session.IdleExpiresAt = now.AddMinutes(-1);
                    break;
                case "absolute-expired":
                    session.IdleExpiresAt = now.AddMinutes(-2);
                    session.AbsoluteExpiresAt = now.AddMinutes(-1);
                    break;
                case "session-revoked":
                    session.RevokedAt = now.AddMinutes(-1);
                    break;
                case "user-disabled":
                    (await setup.Users.SingleAsync(
                        user => user.Id == userId,
                        TestContext.Current.CancellationToken)).IsDisabled = true;
                    break;
            }

            setup.AuthSessions.Add(session);
            await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = database.CreateDbContext();
        var repository = new AuthSessionRepository(context, protector);
        var parsed = protector.ParseAndHash(generated.Value)!;
        var replacement = protector.Generate(Guid.NewGuid());

        var result = await repository.RotateAsync(
            new RefreshRotationCommand(
                parsed.TokenId,
                parsed.SecretHash,
                replacement.TokenId,
                replacement.SecretHash,
                now,
                TimeSpan.FromDays(7),
                "203.0.113.20"),
            TestContext.Current.CancellationToken);

        Assert.Equal(RefreshRotationStatus.Invalid, result.Status);
        Assert.NotNull((await context.AuthSessions.SingleAsync(
            TestContext.Current.CancellationToken)).RevokedAt);
    }

    [Fact]
    public async Task RefreshRejectsWrongSecretWithoutCreatingReplacement()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        var options = Options.Create(new RefreshTokenOptions
        {
            Pepper = "integration-test-pepper-with-at-least-32-bytes",
        });
        var protector = new RefreshTokenProtector(options);
        var generated = protector.Generate(Guid.NewGuid());
        await using (var setup = database.CreateDbContext())
        {
            var userId = await AddUserAsync(setup);
            var session = CreateSession(userId);
            session.RefreshTokens.Add(CreateToken(session.Id, generated.TokenId, generated.SecretHash));
            setup.AuthSessions.Add(session);
            await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = database.CreateDbContext();
        var repository = new AuthSessionRepository(context, protector);
        var replacement = protector.Generate(Guid.NewGuid());
        var result = await repository.RotateAsync(
            new RefreshRotationCommand(
                generated.TokenId,
                Random.Shared.GetItems<byte>([5, 6, 7, 8], 32),
                replacement.TokenId,
                replacement.SecretHash,
                DateTimeOffset.UtcNow,
                TimeSpan.FromDays(7),
                "203.0.113.20"),
            TestContext.Current.CancellationToken);

        Assert.Equal(RefreshRotationStatus.Invalid, result.Status);
        Assert.Single(await context.RefreshTokens.ToArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UserCannotRevokeAnotherUsersSession()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        Guid ownerId;
        Guid attackerId;
        Guid sessionId;
        await using (var setup = database.CreateDbContext())
        {
            ownerId = await AddUserAsync(setup);
            attackerId = await AddUserAsync(setup);
            var session = CreateSession(ownerId);
            sessionId = session.Id;
            session.RefreshTokens.Add(CreateToken(session.Id));
            setup.AuthSessions.Add(session);
            await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = database.CreateDbContext();
        var repository = new AuthSessionRepository(
            context,
            new RefreshTokenProtector(Options.Create(new RefreshTokenOptions
            {
                Pepper = "integration-test-pepper-with-at-least-32-bytes",
            })));

        await repository.RevokeAsync(
            attackerId,
            sessionId,
            DateTimeOffset.UtcNow,
            "session-revoked",
            TestContext.Current.CancellationToken);

        Assert.Null((await context.AuthSessions.SingleAsync(
            TestContext.Current.CancellationToken)).RevokedAt);
    }

    [Fact]
    public async Task RevokeAllRevokesEverySessionAndIncrementsAuthVersion()
    {
        await database.ResetApplicationDataAsync(TestContext.Current.CancellationToken);
        Guid userId;
        await using (var setup = database.CreateDbContext())
        {
            userId = await AddUserAsync(setup);
            var first = CreateSession(userId);
            first.RefreshTokens.Add(CreateToken(first.Id));
            var second = CreateSession(userId);
            second.RefreshTokens.Add(CreateToken(second.Id));
            setup.AuthSessions.AddRange(first, second);
            await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var context = database.CreateDbContext();
        var repository = new AuthSessionRepository(
            context,
            new RefreshTokenProtector(Options.Create(new RefreshTokenOptions
            {
                Pepper = "integration-test-pepper-with-at-least-32-bytes",
            })));

        await repository.RevokeAllAsync(
            userId,
            DateTimeOffset.UtcNow,
            "logout-all",
            TestContext.Current.CancellationToken);

        Assert.All(
            await context.AuthSessions.AsNoTracking().Where(session => session.UserId == userId)
                .ToArrayAsync(TestContext.Current.CancellationToken),
            session => Assert.NotNull(session.RevokedAt));
        Assert.All(
            await context.RefreshTokens.AsNoTracking().Where(token => token.Session.UserId == userId)
                .ToArrayAsync(TestContext.Current.CancellationToken),
            token => Assert.NotNull(token.RevokedAt));
        Assert.Equal(
            1,
            await context.Users.AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => user.AuthVersion)
                .SingleAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<Guid> AddUserAsync(Portfolio.Infrastructure.Persistence.PortfolioDbContext context)
    {
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = $"user-{Guid.NewGuid():N}@example.com",
            NormalizedUserName = $"USER-{Guid.NewGuid():N}@EXAMPLE.COM",
            Email = $"user-{Guid.NewGuid():N}@example.com",
            NormalizedEmail = $"USER-{Guid.NewGuid():N}@EXAMPLE.COM",
        };
        context.Users.Add(user);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user.Id;
    }

    private static AuthSession CreateSession(Guid userId)
    {
        var now = DateTimeOffset.UtcNow;
        return new AuthSession
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = now,
            LastUsedAt = now,
            IdleExpiresAt = now.AddDays(7),
            AbsoluteExpiresAt = now.AddDays(30),
            CreatedIp = "203.0.113.20",
            LastIp = "203.0.113.20",
        };
    }

    private static RefreshToken CreateToken(Guid sessionId, Guid? id = null, byte[]? hash = null) =>
        new()
        {
            Id = id ?? Guid.NewGuid(),
            SessionId = sessionId,
            SecretHash = hash ?? Random.Shared.GetItems<byte>([1, 2, 3, 4], 32),
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedByIp = "203.0.113.20",
        };
}
