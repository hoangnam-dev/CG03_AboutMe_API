namespace Portfolio.Application.Common.Authentication;

public sealed record CurrentAuthSession(Guid UserId, Guid SessionId, int AuthVersion);

public interface ICurrentUserAccessor
{
    CurrentAuthSession GetRequired();
}
