namespace Portfolio.Application.Common.Authentication;

public interface IIdentityAuthenticator
{
    Task<AuthenticatedUser?> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken);
}
