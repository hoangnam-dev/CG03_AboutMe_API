using Portfolio.Application.Common.Exceptions;
using Portfolio.Application.Common.Validation;
using Xunit;

namespace Portfolio.UnitTests.Common.Validation;

public sealed class FileValidationTests
{
    [Fact]
    public async Task RejectsZeroByteFile()
    {
        var upload = CreateUpload([], "empty.png", "image/png", 0);

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => FileValidation.ValidateAsync(
                upload,
                FileValidationOptions.Images(1024),
                TestContext.Current.CancellationToken));

        Assert.Contains("file", exception.Errors.Keys);
    }

    [Fact]
    public async Task RejectsFileOverConfiguredLimit()
    {
        var upload = CreateUpload(PngBytes, "large.png", "image/png", PngBytes.Length);

        var exception = await Assert.ThrowsAsync<PayloadTooLargeException>(
            () => FileValidation.ValidateAsync(
                upload,
                FileValidationOptions.Images(PngBytes.Length - 1),
                TestContext.Current.CancellationToken));

        Assert.Equal("File size exceeds the configured limit.", exception.Message);
    }

    [Theory]
    [InlineData("avatar.jpg", "image/png")]
    [InlineData("avatar.png", "image/jpeg")]
    public async Task RejectsExtensionMimeMismatch(string fileName, string contentType)
    {
        var upload = CreateUpload(PngBytes, fileName, contentType, PngBytes.Length);

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => FileValidation.ValidateAsync(
                upload,
                FileValidationOptions.Images(1024),
                TestContext.Current.CancellationToken));

        Assert.Contains("type", exception.Errors.Keys);
    }

    [Fact]
    public async Task RejectsContentWhoseSignatureDoesNotMatchMetadata()
    {
        var upload = CreateUpload(PdfBytes, "avatar.png", "image/png", PdfBytes.Length);

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => FileValidation.ValidateAsync(
                upload,
                FileValidationOptions.ImagesAndPdf(1024),
                TestContext.Current.CancellationToken));

        Assert.Equal(["File content does not match its declared type."], exception.Errors["file"]);
    }

    [Theory]
    [MemberData(nameof(AllowedFiles))]
    public async Task AcceptsAllowedImageAndPdfSignatures(
        byte[] bytes,
        string fileName,
        string contentType,
        FileKind expectedKind)
    {
        var stream = new MemoryStream(bytes);
        stream.Position = 1;
        var upload = new FileUpload(stream, fileName, contentType, bytes.Length);

        var result = await FileValidation.ValidateAsync(
            upload,
            FileValidationOptions.ImagesAndPdf(1024),
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedKind, result.Kind);
        Assert.Equal(1, stream.Position);
    }

    [Fact]
    public async Task SanitizesUnsafeClientFilenameForMetadataOnly()
    {
        var upload = CreateUpload(PngBytes, "..\\..\\avatar\0 name.png", "image/png", PngBytes.Length);

        var result = await FileValidation.ValidateAsync(
            upload,
            FileValidationOptions.Images(1024),
            TestContext.Current.CancellationToken);

        Assert.Equal("avatar name.png", result.SafeOriginalFileName);
        Assert.Equal(".png", result.Extension);
    }

    [Fact]
    public async Task ReadsSignatureAcrossPartialStreamReads()
    {
        var stream = new ChunkedReadStream(PngBytes);
        var upload = new FileUpload(stream, "avatar.png", "image/png", PngBytes.Length);

        var result = await FileValidation.ValidateAsync(
            upload,
            FileValidationOptions.Images(1024),
            TestContext.Current.CancellationToken);

        Assert.Equal(FileKind.Png, result.Kind);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task AcceptsSafeSvgIcon()
    {
        var bytes = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 24 24\"><path d=\"M1 1h2\"/></svg>"u8.ToArray();
        var upload = CreateUpload(bytes, "icon.svg", "image/svg+xml", bytes.Length);

        var result = await FileValidation.ValidateAsync(
            upload,
            FileValidationOptions.SkillIcons(1024),
            TestContext.Current.CancellationToken);
        await SvgValidation.EnsureSafeAsync(upload.Content, TestContext.Current.CancellationToken);

        Assert.Equal(FileKind.Svg, result.Kind);
    }

    [Fact]
    public async Task AcceptsInertSvgLayerNameMetadata()
    {
        var markup = "<svg xmlns=\"http://www.w3.org/2000/svg\"><g data-name=\"Layer 3\"><path d=\"M1 1h2\"/></g></svg>";
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markup));

        await SvgValidation.EnsureSafeAsync(stream, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task AcceptsProjectIllustrationTextAndAccessibilityMetadata()
    {
        const string markup =
            """
            <svg xmlns="http://www.w3.org/2000/svg" width="1200" height="720" viewBox="0 0 1200 720" role="img" aria-labelledby="title description">
              <title id="title">Internal operations platform illustration</title>
              <desc id="description">A dark dashboard representing task, attendance, human resources, and IT service management modules.</desc>
              <defs><linearGradient id="background" x2="1" y2="1"><stop stop-color="#111827"/><stop offset="1" stop-color="#1e1b4b"/></linearGradient></defs>
              <rect width="1200" height="720" fill="url(#background)"/>
              <rect x="70" y="70" width="1060" height="580" rx="26" fill="#0b1020" stroke="#334155"/>
              <rect x="102" y="104" width="210" height="512" rx="18" fill="#121a2b"/>
              <circle cx="137" cy="139" r="11" fill="#22d3ee"/><text x="163" y="148" fill="#f8fafc" font-family="Arial" font-size="22" font-weight="700">KMS</text>
              <g fill="#475569"><rect x="126" y="205" width="152" height="14" rx="7"/><rect x="126" y="249" width="126" height="14" rx="7"/><rect x="126" y="293" width="141" height="14" rx="7"/><rect x="126" y="337" width="112" height="14" rx="7"/></g>
              <text x="350" y="145" fill="#e2e8f0" font-family="Arial" font-size="25" font-weight="700">KVN Management System</text>
              <g><rect x="350" y="184" width="220" height="150" rx="20" fill="#182236"/><rect x="594" y="184" width="220" height="150" rx="20" fill="#182236"/><rect x="838" y="184" width="220" height="150" rx="20" fill="#182236"/></g>
              <g font-family="Arial" font-size="18" fill="#94a3b8"><text x="380" y="227">TASKS</text><text x="624" y="227">ATTENDANCE</text><text x="868" y="227">IT SERVICES</text></g>
              <g font-family="Arial" font-size="44" font-weight="700" fill="#f8fafc"><text x="380" y="296">128</text><text x="624" y="296">96%</text><text x="868" y="296">24</text></g>
              <rect x="350" y="365" width="708" height="214" rx="20" fill="#182236"/><g fill="#475569"><rect x="382" y="407" width="640" height="16" rx="8"/><rect x="382" y="455" width="570" height="16" rx="8"/><rect x="382" y="503" width="608" height="16" rx="8"/></g>
            </svg>
            """;
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markup));

        await SvgValidation.EnsureSafeAsync(stream, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RejectedSvgReportsTheDisallowedAttributeName()
    {
        const string markup =
            "<svg xmlns=\"http://www.w3.org/2000/svg\"><path style=\"fill:red\" d=\"M1 1h2\"/></svg>";
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markup));

        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            SvgValidation.EnsureSafeAsync(stream, TestContext.Current.CancellationToken));

        Assert.Contains("style", error.Errors["file"].Single(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><script>alert(1)</script></svg>")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><path onload=\"alert(1)\"/></svg>")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><image href=\"https://evil.example/a.png\"/></svg>")]
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\"><path fill=\"url(#safe) url(https://evil.example/a.svg)\"/></svg>")]
    [InlineData("<!DOCTYPE svg [<!ENTITY x SYSTEM \"file:///etc/passwd\">]><svg xmlns=\"http://www.w3.org/2000/svg\"/>")]
    public async Task RejectsActiveSvg(string markup)
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(markup));

        await Assert.ThrowsAsync<ValidationException>(() =>
            SvgValidation.EnsureSafeAsync(stream, TestContext.Current.CancellationToken));
    }

    public static TheoryData<byte[], string, string, FileKind> AllowedFiles => new()
    {
        { PngBytes, "avatar.PNG", "image/png", FileKind.Png },
        { [0xFF, 0xD8, 0xFF, 0xE0, 0x00], "avatar.jpg", "image/jpeg", FileKind.Jpeg },
        { [0xFF, 0xD8, 0xFF, 0xE1, 0x00], "avatar.jpeg", "image/jpeg", FileKind.Jpeg },
        { [0x52, 0x49, 0x46, 0x46, 0, 0, 0, 0, 0x57, 0x45, 0x42, 0x50], "avatar.webp", "image/webp", FileKind.WebP },
        { PdfBytes, "resume.pdf", "application/pdf", FileKind.Pdf },
    };

    private static readonly byte[] PngBytes =
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];

    private static readonly byte[] PdfBytes = "%PDF-1.7"u8.ToArray();

    private static FileUpload CreateUpload(
        byte[] bytes,
        string fileName,
        string contentType,
        long length) => new(new MemoryStream(bytes), fileName, contentType, length);

    private sealed class ChunkedReadStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(1, buffer.Length)], cancellationToken);
    }
}
