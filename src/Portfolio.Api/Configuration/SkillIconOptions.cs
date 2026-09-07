namespace Portfolio.Api.Configuration;

public sealed class SkillIconOptions
{
    public const string SectionName = "SkillIcons";
    public const long MaximumAllowedFileSize = 2 * 1024 * 1024;

    public long MaxFileSize { get; set; } = 512 * 1024;
    public string[] AllowedExternalHosts { get; set; } = [];
}
