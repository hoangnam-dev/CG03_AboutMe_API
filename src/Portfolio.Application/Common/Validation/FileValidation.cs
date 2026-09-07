using Portfolio.Application.Common.Exceptions;

namespace Portfolio.Application.Common.Validation;

public static class FileValidation
{
    private const int MaximumSignatureLength = 12;

    public static async Task<ValidatedFile> ValidateAsync(
        FileUpload upload,
        FileValidationOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(upload);
        ArgumentNullException.ThrowIfNull(options);

        if (upload.Length <= 0)
        {
            throw Invalid("file", "File must not be empty.");
        }

        if (options.MaxFileSize <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(options),
                "Maximum file size must be greater than zero.");
        }

        if (upload.Length > options.MaxFileSize)
        {
            throw new PayloadTooLargeException("File size exceeds the configured limit.");
        }

        if (!upload.Content.CanRead || !upload.Content.CanSeek)
        {
            throw Invalid("file", "File content must be a readable, seekable stream.");
        }

        var extension = Path.GetExtension(upload.OriginalFileName).ToLowerInvariant();
        var kind = ResolveDeclaredKind(extension, upload.ContentType);
        if (kind is null || !options.AllowedKinds.Contains(kind.Value))
        {
            throw Invalid("type", "File extension and content type are not allowed together.");
        }

        var originalPosition = upload.Content.Position;
        var signature = new byte[MaximumSignatureLength];
        var bytesRead = 0;
        try
        {
            upload.Content.Position = 0;
            while (bytesRead < signature.Length)
            {
                var read = await upload.Content.ReadAsync(
                    signature.AsMemory(bytesRead),
                    cancellationToken);
                if (read == 0)
                {
                    break;
                }

                bytesRead += read;
            }
        }
        finally
        {
            upload.Content.Position = originalPosition;
        }

        if (!MatchesSignature(kind.Value, signature.AsSpan(0, bytesRead)))
        {
            throw Invalid("file", "File content does not match its declared type.");
        }

        return new ValidatedFile(
            kind.Value,
            extension,
            CanonicalContentType(kind.Value),
            SanitizeFileName(upload.OriginalFileName),
            upload.Length);
    }

    private static FileKind? ResolveDeclaredKind(string extension, string contentType) =>
        (extension, contentType.ToLowerInvariant()) switch
        {
            (".png", "image/png") => FileKind.Png,
            (".jpg" or ".jpeg", "image/jpeg") => FileKind.Jpeg,
            (".webp", "image/webp") => FileKind.WebP,
            (".pdf", "application/pdf") => FileKind.Pdf,
            _ => null,
        };

    private static bool MatchesSignature(FileKind kind, ReadOnlySpan<byte> bytes) => kind switch
    {
        FileKind.Png => bytes.StartsWith(
            new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        FileKind.Jpeg => bytes.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }),
        FileKind.WebP => bytes.Length >= 12 &&
            bytes[..4].SequenceEqual("RIFF"u8) &&
            bytes.Slice(8, 4).SequenceEqual("WEBP"u8),
        FileKind.Pdf => bytes.StartsWith("%PDF-"u8),
        _ => false,
    };

    private static string CanonicalContentType(FileKind kind) => kind switch
    {
        FileKind.Png => "image/png",
        FileKind.Jpeg => "image/jpeg",
        FileKind.WebP => "image/webp",
        FileKind.Pdf => "application/pdf",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    private static string SanitizeFileName(string fileName)
    {
        var leafName = fileName.Replace('\\', '/').Split('/').LastOrDefault() ?? string.Empty;
        var sanitized = new string(leafName.Where(character => !char.IsControl(character)).ToArray())
            .Trim();

        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return "upload";
        }

        return sanitized.Length <= 255 ? sanitized : sanitized[^255..];
    }

    private static ValidationException Invalid(string field, string message) =>
        new(
            "File validation failed.",
            new Dictionary<string, string[]> { [field] = [message] });
}
