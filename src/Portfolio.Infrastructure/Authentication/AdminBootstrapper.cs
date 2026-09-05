using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Portfolio.Infrastructure.Authentication;

public sealed class AdminBootstrapper(
    RoleManager<IdentityRole<Guid>> roleManager,
    UserManager<ApplicationUser> userManager,
    IOptions<BootstrapAdminOptions> options,
    ILogger<AdminBootstrapper> logger)
{
    public const string AdminRole = "Admin";

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var bootstrap = options.Value;
        if (!bootstrap.Enabled)
        {
            return;
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (!await roleManager.RoleExistsAsync(AdminRole))
        {
            EnsureSucceeded(
                await roleManager.CreateAsync(new IdentityRole<Guid>(AdminRole)),
                "create the Admin role");
        }

        var user = await userManager.FindByEmailAsync(bootstrap.Email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = bootstrap.Email,
                Email = bootstrap.Email,
                EmailConfirmed = true,
            };
            EnsureSucceeded(
                await userManager.CreateAsync(user, bootstrap.Password),
                "create the bootstrap administrator");
        }

        if (!await userManager.IsInRoleAsync(user, AdminRole))
        {
            EnsureSucceeded(
                await userManager.AddToRoleAsync(user, AdminRole),
                "assign the Admin role");
        }

        AdminBootstrapperLog.Completed(logger, user.Id);
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var codes = string.Join(", ", result.Errors.Select(error => error.Code));
        throw new InvalidOperationException($"Unable to {operation}. Identity errors: {codes}");
    }
}

internal static partial class AdminBootstrapperLog
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Information,
        Message = "Administrator bootstrap completed for user {UserId}")]
    public static partial void Completed(ILogger logger, Guid userId);
}
