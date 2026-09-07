namespace Portfolio.Api.Configuration;

public sealed class UploadOptions
{
    public const string SectionName = "Upload";
    public const long MaximumAllowedFileSize = 25 * 1024 * 1024;

    public long MaxFileSize { get; set; } = 10 * 1024 * 1024;
    public int MaxHeroImageWidth { get; set; } = 8192;
    public int MaxHeroImageHeight { get; set; } = 8192;
}
