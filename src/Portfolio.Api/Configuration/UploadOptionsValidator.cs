using Microsoft.Extensions.Options;

namespace Portfolio.Api.Configuration;

public sealed class UploadOptionsValidator : IValidateOptions<UploadOptions>
{
    public ValidateOptionsResult Validate(string? name, UploadOptions options) =>
        options.MaxFileSize is >= 1 and <= UploadOptions.MaximumAllowedFileSize
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"Upload:MaxFileSize must be between 1 and {UploadOptions.MaximumAllowedFileSize} bytes.");
}
