namespace Portfolio.Application.Common.Validation;

public sealed record FileValidationOptions(
    long MaxFileSize,
    IReadOnlySet<FileKind> AllowedKinds)
{
    public static FileValidationOptions Images(long maxFileSize) =>
        new(maxFileSize, new HashSet<FileKind>([FileKind.Png, FileKind.Jpeg, FileKind.WebP]));

    public static FileValidationOptions Pdf(long maxFileSize) =>
        new(maxFileSize, new HashSet<FileKind>([FileKind.Pdf]));

    public static FileValidationOptions ImagesAndPdf(long maxFileSize) =>
        new(maxFileSize, new HashSet<FileKind>(
            [FileKind.Png, FileKind.Jpeg, FileKind.WebP, FileKind.Pdf]));
}
