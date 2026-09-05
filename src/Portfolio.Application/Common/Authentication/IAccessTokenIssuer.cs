namespace Portfolio.Application.Common.Authentication;

public interface IAccessTokenIssuer
{
    AccessToken Issue(AuthenticatedUser user);
}
