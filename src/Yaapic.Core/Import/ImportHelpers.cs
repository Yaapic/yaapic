using System.Buffers;
using System.Text;
using Yaapic.Core.Http;
using Yaapic.Core.Variables;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Import;

/// <summary>Small pure functions shared by the importers.</summary>
public static class ImportHelpers
{
    private const int MaxNameLength = 120;
    private const int MaxDescriptionLength = 80;

    private static readonly SearchValues<char> NameChars =
        SearchValues.Create("abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_-");

    private static readonly string[] ManagedHeaders =
        ["Content-Length", "Host", "Connection", "Accept-Encoding", "Transfer-Encoding", "Keep-Alive", "TE", "Upgrade"];

    /// <summary>Parses an HTTP method name, ignoring case and surrounding white space.</summary>
    /// <param name="text">The method name as written in the source.</param>
    /// <param name="method">The parsed method when the result is <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when the text names one of the seven supported methods.</returns>
    public static bool TryParseMethod(string? text, out HttpMethodKind method) =>
        HttpMethodKindExtensions.TryParse(text?.Trim().ToUpperInvariant(), out method);

    /// <summary>Makes a name safe for a collection item: single line, collapsed spaces, bounded length.</summary>
    /// <param name="name">The source name.</param>
    /// <param name="fallback">The value returned when nothing is left of the name.</param>
    /// <returns>The cleaned name, or <paramref name="fallback"/>.</returns>
    public static string SafeName(string? name, string fallback)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallback;
        }

        StringBuilder sb = new();
        foreach (var c in name.Trim())
        {
            var ch = c is '\r' or '\n' or '\t' ? ' ' : c;
            if (ch == ' ' && sb.Length > 0 && sb[^1] == ' ')
            {
                continue;
            }

            sb.Append(ch);
        }

        var text = sb.ToString();
        if (text.Length > MaxNameLength)
        {
            text = text[..MaxNameLength];
        }

        return text.Length == 0 ? fallback : text;
    }

    /// <summary>Builds a name such as <c>GET {{baseUrl}}/users</c> for a request that has none.</summary>
    /// <param name="method">The request method.</param>
    /// <param name="url">The URL; scheme, query and fragment are dropped.</param>
    /// <returns>The description, at most 80 characters.</returns>
    public static string DescribeRequest(HttpMethodKind method, string url)
    {
        ArgumentNullException.ThrowIfNull(url);

        var text = url;
        var cut = text.IndexOfAny(['?', '#']);
        if (cut >= 0)
        {
            text = text[..cut];
        }

        var scheme = text.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0 && text.IndexOf('/') == scheme + 1)
        {
            text = text[(scheme + 3)..];
        }

        var description = text.Length == 0 ? method.ToText() : $"{method.ToText()} {text}";
        return description.Length > MaxDescriptionLength ? description[..MaxDescriptionLength] : description;
    }

    /// <summary>Splits a URL into the part without query and its query entries.</summary>
    /// <param name="url">The URL as written in the source.</param>
    /// <returns>The URL (prefix and fragment) and the decoded query entries.</returns>
    public static (string Url, IReadOnlyList<KeyValueEntry> Params) SplitUrl(string url)
    {
        var parts = QueryStringSync.Parse(url);
        return (parts.Prefix + parts.Fragment, parts.Params);
    }

    /// <summary>Replaces the <c>:name</c> path segments of a URL by values, or by <c>{{name}}</c> variables.</summary>
    /// <param name="url">The URL; only its path is touched, never the authority or the query.</param>
    /// <param name="values">The known values by name.</param>
    /// <returns>The URL with its path variables replaced.</returns>
    public static string SubstitutePathVariables(string url, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(url);
        ArgumentNullException.ThrowIfNull(values);

        var end = url.IndexOfAny(['?', '#']);
        var head = end < 0 ? url : url[..end];
        var tail = end < 0 ? "" : url[end..];

        var scheme = head.IndexOf("://", StringComparison.Ordinal);
        var authorityStart = scheme >= 0 && head.IndexOf('/') == scheme + 1 ? scheme + 3 : 0;
        var pathStart = head.IndexOf('/', authorityStart);
        if (pathStart < 0)
        {
            return url;
        }

        var segments = head[pathStart..].Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (segment.Length > 1 && segment[0] == ':' && segment.AsSpan(1).IndexOfAnyExcept(NameChars) < 0)
            {
                var name = segment[1..];
                segments[i] = values.TryGetValue(name, out var value) && value.Length > 0 ? value : "{{" + name + "}}";
            }
        }

        return head[..pathStart] + string.Join('/', segments) + tail;
    }

    /// <summary>Maps a MIME type to the raw body type of the editor.</summary>
    /// <param name="mime">The MIME type, possibly with parameters.</param>
    /// <returns>The matching type, <see cref="RawContentType.Text"/> when none matches.</returns>
    public static RawContentType ContentTypeFromMime(string? mime)
    {
        var type = (mime ?? "").Split(';')[0].Trim().ToLowerInvariant();
        if (type.Contains("json", StringComparison.Ordinal))
        {
            return RawContentType.Json;
        }

        if (type.Contains("xml", StringComparison.Ordinal))
        {
            return RawContentType.Xml;
        }

        if (type.Contains("html", StringComparison.Ordinal))
        {
            return RawContentType.Html;
        }

        return type.Contains("javascript", StringComparison.Ordinal) ? RawContentType.JavaScript : RawContentType.Text;
    }

    /// <summary>Tells whether a header is set by the HTTP stack itself and so is dropped on import.</summary>
    /// <param name="name">The header name.</param>
    /// <returns><see langword="true"/> for the connection-level headers and HTTP/2 pseudo-headers.</returns>
    public static bool IsManagedHeader(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return name.StartsWith(':') || ManagedHeaders.Contains(name, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Rewrites the dynamic variables of another tool into Yaapic's and warns about the unknown ones.</summary>
    /// <param name="text">The text to translate; null or empty is returned as is.</param>
    /// <param name="path">Where in the source the text sits, for the report.</param>
    /// <param name="report">Receives one warning per unsupported variable name.</param>
    /// <returns>The translated text.</returns>
    public static string TranslateDynamicVariables(string text, string path, ImportReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        StringBuilder sb = new();
        var last = 0;
        foreach (var span in VariableScanner.ScanSpans(text))
        {
            if (span.Name[0] != '$')
            {
                continue;
            }

            if (span.Name == "$randomUUID")
            {
                sb.Append(text, last, span.Start - last).Append("{{$guid}}");
                last = span.End;
            }
            else if (!DynamicVariableSource.Names.Contains(span.Name))
            {
                report.AddOnce(ImportSeverity.Warning, path, Strings.ImportDynamicVariableUnsupported(span.Name));
            }
        }

        return sb.Append(text, last, text.Length - last).ToString();
    }
}
