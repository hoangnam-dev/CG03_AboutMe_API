using Microsoft.Extensions.Options;

namespace Portfolio.Api.Configuration;

public sealed class UploadOptionsValidator : IValidateOptions<UploadOptions>
{
    public ValidateOptionsResult Validate(string? name, UploadOptions options) =>
        options.MaxFileSize is >= 1 and <= UploadOptions.MaximumAllowedFileSize &&
        options.MaxHeroImageWidth is >= 1 and <= 16_384 &&
        options.MaxHeroImageHeight is >= 1 and <= 16_384 &&
        options.MaxProjectGalleryFiles is >= 1 and <= UploadOptions.MaximumAllowedProjectGalleryFiles
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"Upload:MaxFileSize must be between 1 and {UploadOptions.MaximumAllowedFileSize} bytes; Upload:MaxHeroImageWidth and Upload:MaxHeroImageHeight must be between 1 and 16384 pixels; Upload:MaxProjectGalleryFiles must be between 1 and {UploadOptions.MaximumAllowedProjectGalleryFiles}.");
}
