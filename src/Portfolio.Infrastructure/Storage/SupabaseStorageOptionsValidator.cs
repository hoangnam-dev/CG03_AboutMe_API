using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;

namespace Portfolio.Infrastructure.Storage;

public sealed partial class SupabaseStorageOptionsValidator
    : IValidateOptions<SupabaseStorageOptions>
{
    public ValidateOptionsResult Validate(string? name, SupabaseStorageOptions options)
    {
        var failures = new List<string>();
        if (options.Url is null || !options.Url.IsAbsoluteUri || options.Url.Scheme != Uri.UriSchemeHttps)
        {
            failures.Add("SupabaseStorage:Url must be an absolute HTTPS URL.");
        }

        if (string.IsNullOrWhiteSpace(options.ServiceRoleKey))
        {
            failures.Add("SupabaseStorage:ServiceRoleKey is required.");
        }

        if (options.RequestTimeoutSeconds is < 1 or > 120)
        {
            failures.Add("SupabaseStorage:RequestTimeoutSeconds must be between 1 and 120.");
        }

        if (options.MaxResponseBytes is < 1024 or > 1_048_576)
        {
            failures.Add("SupabaseStorage:MaxResponseBytes must be between 1024 and 1048576.");
        }

        ValidateBucket(options.Buckets.Avatars, "Avatars", failures);
        ValidateBucket(options.Buckets.ProjectImages, "ProjectImages", failures);
        ValidateBucket(options.Buckets.SkillIcons, "SkillIcons", failures);
        ValidateBucket(options.Buckets.CertificateFiles, "CertificateFiles", failures);
        ValidateBucket(options.Buckets.CvFiles, "CvFiles", failures);

        var buckets = new[]
        {
            options.Buckets.Avatars,
            options.Buckets.ProjectImages,
            options.Buckets.SkillIcons,
            options.Buckets.CertificateFiles,
            options.Buckets.CvFiles,
        };
        if (buckets.Distinct(StringComparer.Ordinal).Count() != buckets.Length)
        {
            failures.Add("SupabaseStorage bucket names must be distinct by purpose.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateBucket(
        string bucket,
        string optionName,
        List<string> failures)
    {
        if (string.IsNullOrWhiteSpace(bucket) || !BucketNamePattern().IsMatch(bucket))
        {
            failures.Add($"SupabaseStorage:Buckets:{optionName} is not a valid bucket name.");
        }
    }

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{1,62}[a-z0-9]$", RegexOptions.CultureInvariant)]
    private static partial Regex BucketNamePattern();
}
