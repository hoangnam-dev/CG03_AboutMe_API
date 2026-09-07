using Microsoft.AspNetCore.Identity;

namespace Portfolio.Infrastructure.Authentication;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public int AuthVersion { get; set; }
    public bool IsDisabled { get; set; }
}
