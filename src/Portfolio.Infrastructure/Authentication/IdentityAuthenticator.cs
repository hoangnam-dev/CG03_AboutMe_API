using Microsoft.AspNetCore.Identity;
using Portfolio.Application.Common.Authentication;

namespace Portfolio.Infrastructure.Authentication;

public sealed class IdentityAuthenticator(
    UserManager<ApplicationUser> userManager) : IIdentityAuthenticator
{
    public async Task<AuthenticatedUser?> AuthenticateAsync(
        string email,
        string password,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = await userManager.FindByEmailAsync(email.Trim());
        if (user is null)
        {
            return null;
        }

        if (user.IsDisabled ||
            userManager.SupportsUserLockout && await userManager.IsLockedOutAsync(user))
        {
            return null;
        }

        if (!await userManager.CheckPasswordAsync(user, password))
        {
            if (userManager.SupportsUserLockout)
            {
                await userManager.AccessFailedAsync(user);
            }

            return null;
        }

        if (userManager.SupportsUserLockout)
        {
            await userManager.ResetAccessFailedCountAsync(user);
        }

        var roles = await userManager.GetRolesAsync(user);
        cancellationToken.ThrowIfCancellationRequested();
        return new AuthenticatedUser(user.Id, user.Email!, roles.ToArray(), user.AuthVersion);
    }
}
