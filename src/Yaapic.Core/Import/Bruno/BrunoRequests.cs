using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Import.Bruno;

/// <summary>How the authentication of a Bruno request was resolved.</summary>
internal enum BruAuthMode
{
    /// <summary>The request has no authentication.</summary>
    None,

    /// <summary>The request inherits the authentication of its folder or collection.</summary>
    Inherit,

    /// <summary>The authentication was mapped from the request file.</summary>
    Mapped,
}

/// <summary>Maps one <c>.bru</c> request file to an <see cref="HttpRequestDefinition"/>.</summary>
internal static class BrunoRequests
{
    private const string FileMarker = "@file(";
    private const string ContentType = "Content-Type";
    private const string ProcessEnv = "{{process.env.";

    private static readonly string[] Verbs = ["get", "post", "put", "patch", "delete", "head", "options"];

    /// <summary>Maps a parsed request file.</summary>
    /// <param name="file">The parsed file.</param>
    /// <param name="path">The report path of the request.</param>
    /// <param name="id">The identifier of the new request.</param>
    /// <param name="report">Receives the warnings and infos.</param>
    /// <param name="authMode">How the authentication was resolved; the caller replaces the auth on <see cref="BruAuthMode.Inherit"/>.</param>
    /// <returns>The request, or null (with a skipped entry) when the file is not a supported HTTP request.</returns>
    internal static HttpRequestDefinition? Map(BruFile file, string path, Guid id, ImportReport report, out BruAuthMode authMode)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(report);
        authMode = BruAuthMode.None;

        var verbBlock = file.Blocks.FirstOrDefault(b => Verbs.Contains(b.Name, StringComparer.Ordinal))
            ?? file.Blocks.FirstOrDefault(b => b.Name is "trace" or "connect");
        if (verbBlock is null)
        {
            report.Skipped(path, Strings.ImportPostmanItemUnknown(path));
            return null;
        }

        if (!ImportHelpers.TryParseMethod(verbBlock.Name, out var method))
        {
            report.Skipped(path, Strings.ImportMethodUnsupported(verbBlock.Name.ToUpperInvariant()));
            return null;
        }

        var meta = file.Get("meta");
        var type = Value(meta, "type");
        if (type is { Length: > 0 } && type is not ("http" or "graphql"))
        {
            report.Skipped(path, Strings.ImportBrunoTypeUnsupported(path, type));
            return null;
        }

        var (baseUrl, urlParams) = ImportHelpers.SplitUrl(Value(verbBlock, "url") ?? "");
        var query = file.Get("params:query") is { } q ? Entries(q) : [.. urlParams];
        Dictionary<string, string> pathValues = [];
        foreach (var p in file.Get("params:path")?.Pairs ?? [])
        {
            pathValues.TryAdd(p.Key, p.Value);
        }

        var url = ImportHelpers.SubstitutePathVariables(baseUrl, pathValues);
        var headers = Entries(file.Get("headers"));
        var body = ReadBody(file, Value(verbBlock, "body"), path, report, headers);
        var auth = BrunoAuth.Map(file, Value(verbBlock, "auth"), path, report, out authMode);

        if (file.Blocks.Where(b => !IsCode(b.Name)).Any(Mentions))
        {
            report.AddOnce(ImportSeverity.Warning, path, Strings.ImportBrunoProcessEnv);
        }

        if (file.Get("docs") is not null)
        {
            report.Info(path, Strings.ImportDescriptionsDropped(1));
        }

        if (file.Get("assert") is { } assert)
        {
            report.Warning(path, Strings.ImportBrunoAssertDropped(assert.Pairs.Count));
        }

        return new HttpRequestDefinition(
            id,
            ImportHelpers.SafeName(Value(meta, "name"), ImportHelpers.DescribeRequest(method, url)),
            method,
            url,
            query,
            headers,
            body,
            auth,
            RequestSettings.Default);
    }

    private static string? Value(BruBlock? block, string key) =>
        block?.Pairs.FirstOrDefault(p => p.Key == key)?.Value;

    private static List<KeyValueEntry> Entries(BruBlock? block) =>
        [.. (block?.Pairs ?? []).Select(p => new KeyValueEntry(p.Key, p.Value, p.Enabled))];

    private static List<FormField> Fields(BruBlock? block) =>
        [.. (block?.Pairs ?? []).Select(p => new FormField(p.Key, p.Value, p.Enabled))];

    private static bool IsCode(string name) =>
        name.StartsWith("script:", StringComparison.Ordinal) || name is "tests" or "docs" or "assert" or "meta";

    private static bool Mentions(BruBlock block) =>
        block.Text.Contains(ProcessEnv, StringComparison.Ordinal)
        || block.Pairs.Any(p => p.Value.Contains(ProcessEnv, StringComparison.Ordinal));

    private static string? FilePath(string value)
    {
        if (!value.StartsWith(FileMarker, StringComparison.Ordinal))
        {
            return null;
        }

        var end = value.IndexOf(')', FileMarker.Length);
        return end < 0 ? value[FileMarker.Length..] : value[FileMarker.Length..end];
    }

    private static RequestBody ReadBody(BruFile file, string? mode, string path, ImportReport report, List<KeyValueEntry> headers)
    {
        string Text(string name) => file.Get(name)?.Text ?? "";

        switch (mode)
        {
            case null or "" or "none":
                return RequestBody.None;
            case "json":
                return RequestBody.Raw(RawContentType.Json, Text("body:json"));
            case "text":
                return RequestBody.Raw(RawContentType.Text, Text("body:text"));
            case "xml":
                return RequestBody.Raw(RawContentType.Xml, Text("body:xml"));
            case "sparql":
                AddContentType(headers, "application/sparql-query");
                return RequestBody.Raw(RawContentType.Text, Text("body:sparql"));
            case "graphql":
                AddContentType(headers, "application/json");
                report.AddOnce(ImportSeverity.Info, path, Strings.ImportBrunoGraphQl);
                return RequestBody.Raw(RawContentType.Json, GraphQl(Text("body:graphql"), Text("body:graphql:vars")));
            case "formUrlEncoded":
                return RequestBody.UrlEncoded(Fields(file.Get("body:form-urlencoded")));
            case "multipartForm":
                return RequestBody.FormData(MultipartFields(file.Get("body:multipart-form"), path, report));
            case "file":
                var reference = file.Get("body:file")?.Pairs.Select(p => FilePath(p.Value)).FirstOrDefault(f => f is not null);
                if (reference is null)
                {
                    return RequestBody.None;
                }

                report.Warning(path, Strings.ImportFileReference(path, reference));
                return RequestBody.Binary(reference);
            default:
                report.Warning(path, Strings.ImportBrunoBodyModeUnsupported(path, mode));
                return RequestBody.None;
        }
    }

    private static void AddContentType(List<KeyValueEntry> headers, string value)
    {
        if (!headers.Exists(h => h.Key.Equals(ContentType, StringComparison.OrdinalIgnoreCase)))
        {
            headers.Add(new KeyValueEntry(ContentType, value));
        }
    }

    private static List<FormField> MultipartFields(BruBlock? block, string path, ImportReport report)
    {
        List<FormField> fields = [];
        foreach (var p in block?.Pairs ?? [])
        {
            if (FilePath(p.Value) is { } file)
            {
                report.Warning(path, Strings.ImportFileReference(path, file));
                fields.Add(new FormField(p.Key, file, p.Enabled, FormFieldKind.File));
            }
            else
            {
                fields.Add(new FormField(p.Key, p.Value, p.Enabled));
            }
        }

        return fields;
    }

    private static string GraphQl(string query, string variables)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteString("query", query);
            writer.WritePropertyName("variables");
            if (!TryParseJson(variables, out var parsed))
            {
                writer.WriteStartObject();
                writer.WriteEndObject();
            }
            else
            {
                using (parsed)
                {
                    parsed!.RootElement.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    private static bool TryParseJson(string text, out JsonDocument? document)
    {
        document = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            document = JsonDocument.Parse(text);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
