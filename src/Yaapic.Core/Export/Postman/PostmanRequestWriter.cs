using System.Text;
using Yaapic.Core.Http;
using Yaapic.Core.Import;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Export.Postman;

/// <summary>Writes the <c>request</c> object of a Postman v2.1 item.</summary>
internal static class PostmanRequestWriter
{
    /// <summary>Writes a request.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The <c>request</c> node.</returns>
    internal static DataNode Write(HttpRequestDefinition request)
    {
        List<(string, DataNode)> members =
        [
            ("method", DataNodeJson.Str(request.Method.ToText())),
            ("header", DataNodeJson.Arr([.. request.Headers.Select(h => Pair(h.Key, h.Value, h.Enabled, withType: true))])),
        ];
        if (Body(request.Body) is { } body)
        {
            members.Add(("body", body));
        }

        members.Add(("url", Url(request.Url, request.QueryParams)));
        return DataNodeJson.Obj([.. members]);
    }

    /// <summary>Tells whether a request refers to a local file (a file form field or a binary body).</summary>
    /// <param name="request">The request.</param>
    /// <returns><see langword="true"/> when a path is exported.</returns>
    internal static bool ReferencesFile(HttpRequestDefinition request) =>
        request.Body.Kind == BodyKind.Binary
        || (request.Body.Kind == BodyKind.FormData && request.Body.Fields.Any(f => f.Kind == FormFieldKind.File));

    private static DataNode Pair(string key, string value, bool enabled, bool withType)
    {
        List<(string, DataNode)> members = [("key", DataNodeJson.Str(key)), ("value", DataNodeJson.Str(value))];
        if (!enabled) members.Add(("disabled", DataNode.Bool(true)));
        if (withType) members.Add(("type", DataNodeJson.Str("text")));
        return DataNodeJson.Obj([.. members]);
    }

    private static DataNode? Body(RequestBody body) => body.Kind switch
    {
        BodyKind.Raw => DataNodeJson.Obj(
            ("mode", DataNodeJson.Str("raw")),
            ("raw", DataNodeJson.Str(body.Text)),
            ("options", DataNodeJson.Obj(("raw", DataNodeJson.Obj(("language", DataNodeJson.Str(Language(body.ContentType)))))))),
        BodyKind.UrlEncoded => DataNodeJson.Obj(
            ("mode", DataNodeJson.Str("urlencoded")),
            ("urlencoded", DataNodeJson.Arr([.. body.Fields.Select(f => Pair(f.Key, f.Value, f.Enabled, withType: true))]))),
        BodyKind.FormData => DataNodeJson.Obj(
            ("mode", DataNodeJson.Str("formdata")),
            ("formdata", DataNodeJson.Arr([.. body.Fields.Select(FormDataField)]))),
        BodyKind.Binary => DataNodeJson.Obj(
            ("mode", DataNodeJson.Str("file")),
            ("file", DataNodeJson.Obj(("src", DataNodeJson.Str(body.FilePath))))),
        _ => null,
    };

    private static DataNode FormDataField(FormField f)
    {
        if (f.Kind != FormFieldKind.File) return Pair(f.Key, f.Value, f.Enabled, withType: true);
        List<(string, DataNode)> members = [("key", DataNodeJson.Str(f.Key)), ("src", DataNodeJson.Str(f.Value))];
        if (!f.Enabled) members.Add(("disabled", DataNode.Bool(true)));
        members.Add(("type", DataNodeJson.Str("file")));
        return DataNodeJson.Obj([.. members]);
    }

    private static string Language(RawContentType type) => type switch
    {
        RawContentType.Json => "json",
        RawContentType.Xml => "xml",
        RawContentType.Html => "html",
        RawContentType.JavaScript => "javascript",
        _ => "text",
    };

    private static DataNode Url(string url, IReadOnlyList<KeyValueEntry> query)
    {
        var parts = QueryStringSync.Parse(url);
        var raw = QueryStringSync.Compose(parts.Prefix, [.. query.Where(q => q.Enabled)], parts.Fragment);

        var rest = parts.Prefix;
        string? protocol = null;
        var scheme = rest.IndexOf("://", StringComparison.Ordinal);
        if (scheme > 0 && rest[..scheme].All(c => char.IsAsciiLetterOrDigit(c) || c is '+' or '-' or '.'))
        {
            protocol = rest[..scheme];
            rest = rest[(scheme + 3)..];
        }

        var slash = IndexOutsideTokens(rest, '/');
        var authority = slash < 0 ? rest : rest[..slash];
        var path = slash < 0 ? "" : rest[(slash + 1)..];

        string? port = null;
        var colon = authority.LastIndexOf(':');
        if (colon >= 0 && colon + 1 < authority.Length && authority[(colon + 1)..].All(char.IsAsciiDigit))
        {
            port = authority[(colon + 1)..];
            authority = authority[..colon];
        }

        List<(string, DataNode)> members = [("raw", DataNodeJson.Str(raw))];
        if (protocol is not null) members.Add(("protocol", DataNodeJson.Str(protocol)));
        members.Add(("host", DataNodeJson.Arr([.. SplitOutsideTokens(authority, '.').Select(DataNodeJson.Str)])));
        if (port is not null) members.Add(("port", DataNodeJson.Str(port)));
        var segments = SplitOutsideTokens(path, '/').Where(s => s.Length > 0).ToArray();
        if (segments.Length > 0) members.Add(("path", DataNodeJson.Arr([.. segments.Select(DataNodeJson.Str)])));
        if (query.Count > 0) members.Add(("query", DataNodeJson.Arr([.. query.Select(q => Pair(q.Key, q.Value, q.Enabled, withType: false))])));
        return DataNodeJson.Obj([.. members]);
    }

    private static int IndexOutsideTokens(string text, char separator)
    {
        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text.AsSpan(i).StartsWith("{{")) { depth++; i++; }
            else if (depth > 0 && text.AsSpan(i).StartsWith("}}")) { depth--; i++; }
            else if (depth == 0 && text[i] == separator) return i;
        }

        return -1;
    }

    private static List<string> SplitOutsideTokens(string text, char separator)
    {
        List<string> parts = [];
        StringBuilder current = new();
        var depth = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text.AsSpan(i).StartsWith("{{")) { depth++; current.Append("{{"); i++; }
            else if (depth > 0 && text.AsSpan(i).StartsWith("}}")) { depth--; current.Append("}}"); i++; }
            else if (depth == 0 && text[i] == separator) { parts.Add(current.ToString()); current.Clear(); }
            else current.Append(text[i]);
        }

        parts.Add(current.ToString());
        return parts;
    }
}
