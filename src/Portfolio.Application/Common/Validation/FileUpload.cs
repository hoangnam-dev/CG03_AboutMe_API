namespace Portfolio.Application.Common.Validation;

public sealed record FileUpload(
    Stream Content,
    string OriginalFileName,
    string ContentType,
    long Length);

public sealed record ValidatedFile(
    FileKind Kind,
    string Extension,
    string ContentType,
    string SafeOriginalFileName,
    long Length);

public enum FileKind
{
    Png,
    Jpeg,
    WebP,
    Pdf,
}
