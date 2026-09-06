using System.Buffers.Binary;
using Portfolio.Application.Common.Exceptions;

namespace Portfolio.Application.Common.Validation;

public static class ImageDimensionValidation
{
    public static async Task EnsureWithinAsync(
        Stream content,
        FileKind kind,
        int maximumWidth,
        int maximumHeight,
        CancellationToken cancellationToken)
    {
        if (maximumWidth <= 0 || maximumHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumWidth));

        var originalPosition = content.Position;
        try
        {
            content.Position = 0;
            using var buffer = new MemoryStream();
            await content.CopyToAsync(buffer, cancellationToken);
            var dimensions = kind switch
            {
                FileKind.Png => ReadPng(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length))),
                FileKind.Jpeg => ReadJpeg(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length))),
                FileKind.WebP => ReadWebP(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length))),
                _ => null,
            };
            if (dimensions is null)
                throw Invalid("Image dimensions could not be read.");
            if (dimensions.Value.Width > maximumWidth || dimensions.Value.Height > maximumHeight)
                throw Invalid($"Image dimensions must not exceed {maximumWidth}x{maximumHeight} pixels.");
        }
        finally
        {
            content.Position = originalPosition;
        }
    }

    private static (int Width, int Height)? ReadPng(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 24) return null;
        var width = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(20, 4));
        return ToDimensions(width, height);
    }

    private static (int Width, int Height)? ReadJpeg(ReadOnlySpan<byte> bytes)
    {
        var offset = 2;
        while (offset + 3 < bytes.Length)
        {
            if (bytes[offset] != 0xFF) { offset++; continue; }
            while (offset < bytes.Length && bytes[offset] == 0xFF) offset++;
            if (offset >= bytes.Length) return null;
            var marker = bytes[offset++];
            if (marker is 0xD8 or 0xD9) continue;
            if (offset + 2 > bytes.Length) return null;
            var length = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset, 2));
            if (length < 2 || offset + length > bytes.Length) return null;
            if (marker is 0xC0 or 0xC1 or 0xC2 or 0xC3 or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF)
            {
                if (length < 7) return null;
                var height = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 3, 2));
                var width = BinaryPrimitives.ReadUInt16BigEndian(bytes.Slice(offset + 5, 2));
                return ToDimensions(width, height);
            }
            offset += length;
        }
        return null;
    }

    private static (int Width, int Height)? ReadWebP(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 30) return null;
        var type = bytes.Slice(12, 4);
        if (type.SequenceEqual("VP8X"u8))
        {
            var width = 1u + ReadUInt24LittleEndian(bytes.Slice(24, 3));
            var height = 1u + ReadUInt24LittleEndian(bytes.Slice(27, 3));
            return ToDimensions((uint)width, (uint)height);
        }
        if (type.SequenceEqual("VP8L"u8) && bytes.Length >= 25 && bytes[20] == 0x2F)
        {
            var bits = BinaryPrimitives.ReadUInt32LittleEndian(bytes.Slice(21, 4));
            return ((int)((bits & 0x3FFF) + 1), (int)(((bits >> 14) & 0x3FFF) + 1));
        }
        if (type.SequenceEqual("VP8 "u8) && bytes.Length >= 30 &&
            bytes.Slice(23, 3).SequenceEqual(new byte[] { 0x9D, 0x01, 0x2A }))
        {
            var width = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(26, 2)) & 0x3FFF;
            var height = BinaryPrimitives.ReadUInt16LittleEndian(bytes.Slice(28, 2)) & 0x3FFF;
            return ToDimensions((uint)width, (uint)height);
        }
        return null;
    }

    private static uint ReadUInt24LittleEndian(ReadOnlySpan<byte> bytes) =>
        (uint)(bytes[0] | bytes[1] << 8 | bytes[2] << 16);

    private static (int Width, int Height)? ToDimensions(uint width, uint height) =>
        width is > 0 and <= int.MaxValue && height is > 0 and <= int.MaxValue
            ? ((int)width, (int)height)
            : null;

    private static ValidationException Invalid(string message) =>
        new("Image validation failed.", new Dictionary<string, string[]> { ["file"] = [message] });
}
