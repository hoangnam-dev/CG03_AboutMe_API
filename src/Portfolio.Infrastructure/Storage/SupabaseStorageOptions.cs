namespace Portfolio.Infrastructure.Storage;

public sealed class SupabaseStorageOptions
{
    public const string SectionName = "SupabaseStorage";

    public Uri? Url { get; set; }
    public string ServiceRoleKey { get; set; } = string.Empty;
    public int RequestTimeoutSeconds { get; set; } = 30;
    public int MaxResponseBytes { get; set; } = 65_536;
    public StorageBucketOptions Buckets { get; set; } = new();
}

public sealed class StorageBucketOptions
{
    public string Avatars { get; set; } = "avatars";
    public string ProjectImages { get; set; } = "project-images";
    public string SkillIcons { get; set; } = "skill-icons";
    public string CertificateFiles { get; set; } = "certificate-files";
    public string CvFiles { get; set; } = "cv-files";
}
