using Microsoft.Extensions.Options;
using Portfolio.Application.Common.Authentication;

namespace Portfolio.Infrastructure.Authentication;

public sealed class AuthSessionLifetime(IOptions<RefreshTokenOptions> options) : IAuthSessionLifetime
{
    public TimeSpan IdleLifetime { get; } = TimeSpan.FromDays(options.Value.IdleLifetimeDays);
    public TimeSpan AbsoluteLifetime { get; } = TimeSpan.FromDays(options.Value.AbsoluteLifetimeDays);
}
