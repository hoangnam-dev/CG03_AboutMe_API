namespace Portfolio.Application.Common.Validation;

public sealed record FileValidationOptions(
    long MaxFileSize,
    IReadOnlySet<FileKind> AllowedKinds)
{
    public static FileValidationOptions Images(long maxFileSize) =>
        new(maxFileSize, new HashSet<FileKind>([FileKind.Png, FileKind.Jpeg, FileKind.WebP, FileKind.Svg]));

    public static FileValidationOptions Pdf(long maxFileSize) =>
        new(maxFileSize, new HashSet<FileKind>([FileKind.Pdf]));

    public static FileValidationOptions ImagesAndPdf(long maxFileSize) =>
        new(maxFileSize, new HashSet<FileKind>(
            [FileKind.Png, FileKind.Jpeg, FileKind.WebP, FileKind.Pdf]));

    public static FileValidationOptions SkillIcons(long maxFileSize) =>
        new(maxFileSize, new HashSet<FileKind>(
            [FileKind.Png, FileKind.Jpeg, FileKind.WebP, FileKind.Svg]));
}
