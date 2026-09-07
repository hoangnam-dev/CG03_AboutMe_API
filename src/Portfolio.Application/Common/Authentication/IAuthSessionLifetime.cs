namespace Portfolio.Application.Common.Authentication;

public interface IAuthSessionLifetime
{
    TimeSpan IdleLifetime { get; }
    TimeSpan AbsoluteLifetime { get; }
}
