using Microsoft.Extensions.Options;

namespace Portfolio.Api.Configuration;

public sealed class SkillIconOptionsValidator : IValidateOptions<SkillIconOptions>
{
    public ValidateOptionsResult Validate(string? name, SkillIconOptions options)
    {
        if (options.MaxFileSize is < 1 or > SkillIconOptions.MaximumAllowedFileSize)
            return ValidateOptionsResult.Fail(
                $"SkillIcons:MaxFileSize must be between 1 and {SkillIconOptions.MaximumAllowedFileSize} bytes.");
        if (options.AllowedExternalHosts.Any(host =>
                string.IsNullOrWhiteSpace(host) ||
                host.Contains('/') || host.Contains(':') || host.Contains('@')))
            return ValidateOptionsResult.Fail("SkillIcons:AllowedExternalHosts must contain host names only.");
        return ValidateOptionsResult.Success;
    }
}
