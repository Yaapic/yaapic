using System.Globalization;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Import.Har;

/// <summary>Converts one HAR entry into a request.</summary>
internal static class HarEntries
{
    private static readonly HashSet<string> RawTypes = new(StringComparer.Ordinal)
    {
        "application/json", "application/xml", "text/xml", "text/html", "text/plain", "application/javascript", "text/javascript",
    };

    /// <summary>Converts an entry; returns <see langword="null"/> (after reporting it) when it cannot become a request.</summary>
    internal static HttpRequestDefinition? Convert(DataNode entry, int index, ImportContext context)
    {
        var path = "entries[" + index.ToString(CultureInfo.InvariantCulture) + "]";
        var request = entry["request"];
        var url = request?.GetString("url");
        if (request is null || string.IsNullOrEmpty(url))
        {
            context.Report.Skipped(path, Strings.ImportHarNoUrl(index));
            return null;
        }

        var methodText = request.GetString("method") ?? "GET";
        if (!ImportHelpers.TryParseMethod(methodText, out var method))
        {
            context.Report.Skipped(path, Strings.ImportMethodUnsupported(methodText));
            return null;
        }

        var (cleanUrl, query) = ImportHelpers.SplitUrl(url);
        var name = ImportHelpers.DescribeRequest(method, cleanUrl);
        List<KeyValueEntry> headers = [];
        foreach (var header in request["headers"]?.Items ?? [])
        {
            if (header.GetString("name") is { Length: > 0 } key && !ImportHelpers.IsManagedHeader(key))
            {
                headers.Add(new KeyValueEntry(key, header.GetString("value") ?? ""));
            }
        }

        var body = ReadBody(request["postData"], name, headers, context);
        return new HttpRequestDefinition(
            context.NewId(), name, method, cleanUrl, query, headers, body, RequestAuth.None, RequestSettings.Default);
    }

    private static RequestBody ReadBody(DataNode? post, string name, List<KeyValueEntry> headers, ImportContext context)
    {
        if (post is not { IsObject: true })
        {
            return RequestBody.None;
        }

        if (post.GetString("encoding") == "base64"
            || post.GetString("comment")?.Contains("base64", StringComparison.OrdinalIgnoreCase) == true)
        {
            context.Report.Warning(name, Strings.ImportHarBase64Body(name));
            return RequestBody.None;
        }

        var mime = (post.GetString("mimeType") ?? "").Split(';')[0].Trim().ToLowerInvariant();
        var text = post.GetString("text");
        var parameters = post["params"]?.Items ?? [];

        if (mime == "application/x-www-form-urlencoded")
        {
            if (parameters.Count > 0)
            {
                return RequestBody.UrlEncoded([.. parameters.Select(p => new FormField(p.GetString("name") ?? "", p.GetString("value") ?? ""))]);
            }

            return string.IsNullOrEmpty(text) ? RequestBody.None : RequestBody.UrlEncoded(ParseFields(text));
        }

        if (mime == "multipart/form-data")
        {
            headers.RemoveAll(h => h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase));
            List<FormField> fields = [];
            foreach (var p in parameters)
            {
                var key = p.GetString("name") ?? "";
                if (p.GetString("fileName") is { } fileName)
                {
                    fields.Add(new FormField(key, "", true, FormFieldKind.File));
                    context.Report.Warning(name, Strings.ImportHarFileField(name, key, fileName));
                }
                else
                {
                    fields.Add(new FormField(key, p.GetString("value") ?? ""));
                }
            }

            return RequestBody.FormData(fields);
        }

        if (mime.Length > 0 && !RawTypes.Contains(mime)
            && !headers.Exists(h => h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)))
        {
            headers.Add(new KeyValueEntry("Content-Type", mime));
        }

        return text is null ? RequestBody.None : RequestBody.Raw(ImportHelpers.ContentTypeFromMime(mime), text);
    }

    private static List<FormField> ParseFields(string text) =>
        [.. text.Split('&', StringSplitOptions.RemoveEmptyEntries).Select(piece =>
        {
            var eq = piece.IndexOf('=', StringComparison.Ordinal);
            return eq < 0 ? new FormField(Decode(piece), "") : new FormField(Decode(piece[..eq]), Decode(piece[(eq + 1)..]));
        })];

    private static string Decode(string text)
    {
        try
        {
            return Uri.UnescapeDataString(text.Replace('+', ' '));
        }
        catch (UriFormatException)
        {
            return text;
        }
    }
}
