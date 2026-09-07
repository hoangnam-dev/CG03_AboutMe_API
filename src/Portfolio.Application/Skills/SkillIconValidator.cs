using System.Text.RegularExpressions;
using Portfolio.Application.Common.Exceptions;

namespace Portfolio.Application.Skills;

public static partial class SkillIconValidator
{
    public static readonly IReadOnlySet<string> LucideAllowlist =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "atom", "blocks", "boxes", "code-xml", "database", "database-zap",
            "git-branch", "layers", "panel-top", "triangle", "wind",
        };

    public static SkillIconRequest Validate(SkillIconRequest icon, SkillIconSettings settings)
    {
        ArgumentNullException.ThrowIfNull(icon);
        ArgumentNullException.ThrowIfNull(settings);
        var type = icon.Type?.Trim().ToLowerInvariant() ?? string.Empty;
        var value = icon.Value?.Trim() ?? string.Empty;

        switch (type)
        {
            case "text" when value.Length is >= 1 and <= 6:
                break;
            case "text":
                throw Invalid("icon.value", "Text icons must contain between one and six characters.");
            case "lucide" when value.Length <= 100 && LucideName().IsMatch(value) && LucideAllowlist.Contains(value):
                break;
            case "lucide":
                throw Invalid("icon.value", "Lucide icon name is not on the approved allowlist.");
            case "image" when IsAllowedImage(value, settings):
                break;
            case "image":
                throw Invalid("icon.value", "Image icons must be managed skill-icon URLs or HTTPS URLs on an allowed host.");
            default:
                throw Invalid("icon.type", "Icon type must be 'lucide', 'image', or 'text'.");
        }

        return new SkillIconRequest(type, value);
    }

    private static bool IsAllowedImage(string value, SkillIconSettings settings)
    {
        if (value.Length is < 1 or > 500 || value.Contains('<') || value.Contains('>')) return false;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return false;
        if (uri.UserInfo.Length > 0) return false;
        if (Path.GetExtension(uri.AbsolutePath).ToLowerInvariant() is not (".png" or ".jpg" or ".jpeg" or ".webp" or ".svg"))
            return false;
        if (settings.AllowedExternalHosts.Contains(uri.Host)) return true;
        var managedMarker = $"/storage/v1/object/public/{settings.Bucket}/skills/";
        return !string.IsNullOrWhiteSpace(settings.ManagedStorageHost) &&
               string.Equals(uri.Host, settings.ManagedStorageHost, StringComparison.OrdinalIgnoreCase) &&
               uri.AbsolutePath.StartsWith(managedMarker, StringComparison.Ordinal);
    }

    private static ValidationException Invalid(string field, string message) =>
        new("Skill icon validation failed.", new Dictionary<string, string[]> { [field] = [message] });

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex LucideName();
}
