using System.Text.RegularExpressions;
using System.Xml;
using Portfolio.Application.Common.Exceptions;

namespace Portfolio.Application.Common.Validation;

public static partial class SvgValidation
{
    private const string SvgNamespace = "http://www.w3.org/2000/svg";
    private static readonly HashSet<string> AllowedElements = new(StringComparer.Ordinal)
    {
        "svg", "g", "path", "circle", "ellipse", "rect", "line", "polyline", "polygon",
        "title", "desc", "defs", "linearGradient", "radialGradient", "stop", "clipPath",
    };
    private static readonly HashSet<string> AllowedAttributes = new(StringComparer.Ordinal)
    {
        "xmlns", "viewBox", "width", "height", "x", "y", "x1", "x2", "y1", "y2",
        "cx", "cy", "r", "rx", "ry", "d", "points", "fill", "fill-rule", "stroke",
        "stroke-width", "stroke-linecap", "stroke-linejoin", "stroke-miterlimit",
        "stroke-dasharray", "stroke-dashoffset", "opacity", "fill-opacity", "stroke-opacity",
        "transform", "id", "offset", "stop-color", "stop-opacity", "gradientUnits",
        "gradientTransform", "fx", "fy", "fr", "clip-path", "clip-rule", "role", "aria-label",
    };

    public static async Task EnsureSafeAsync(Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (!content.CanRead || !content.CanSeek) throw Invalid("SVG content must be readable and seekable.");
        var originalPosition = content.Position;
        var rootSeen = false;
        try
        {
            content.Position = 0;
            using var reader = XmlReader.Create(content, new XmlReaderSettings
            {
                Async = true,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreProcessingInstructions = true,
                MaxCharactersInDocument = 2 * 1024 * 1024,
            });
            while (await reader.ReadAsync())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.NodeType != XmlNodeType.Element) continue;
                if (!rootSeen)
                {
                    rootSeen = true;
                    if (reader.LocalName != "svg") throw Invalid("SVG root element is required.");
                }
                if (reader.NamespaceURI != SvgNamespace || !AllowedElements.Contains(reader.LocalName))
                    throw Invalid("SVG contains a disallowed element or namespace.");
                if (!reader.HasAttributes) continue;
                while (reader.MoveToNextAttribute())
                {
                    if (reader.Prefix == "xmlns" || reader.Name == "xmlns") continue;
                    if (reader.NamespaceURI.Length > 0 ||
                        reader.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase) ||
                        !AllowedAttributes.Contains(reader.LocalName) ||
                        reader.Value.Contains("javascript:", StringComparison.OrdinalIgnoreCase) ||
                        (reader.Value.Contains("url(", StringComparison.OrdinalIgnoreCase) &&
                         !LocalUrlReference().IsMatch(reader.Value.Trim())))
                        throw Invalid("SVG contains a disallowed or external attribute.");
                }
                reader.MoveToElement();
            }
            if (!rootSeen) throw Invalid("SVG root element is required.");
        }
        catch (XmlException)
        {
            throw Invalid("SVG must be well-formed and must not contain a DTD or entity declaration.");
        }
        finally
        {
            content.Position = originalPosition;
        }
    }

    private static ValidationException Invalid(string message) =>
        new("SVG validation failed.", new Dictionary<string, string[]> { ["file"] = [message] });

    [GeneratedRegex("^url\\(#[A-Za-z_][A-Za-z0-9_.:-]*\\)$", RegexOptions.CultureInvariant)]
    private static partial Regex LocalUrlReference();
}
