using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Import.OpenApi;

/// <summary>Maps the <c>requestBody</c> of an operation to a Yaapic body.</summary>
internal static class OpenApiBodies
{
    private static readonly string[] SelfTyped =
    [
        "application/json", "application/xml", "text/xml", "text/html", "text/plain",
        "application/javascript", "text/javascript", "application/x-www-form-urlencoded", "multipart/form-data",
    ];

    /// <summary>Maps a request body.</summary>
    /// <param name="requestBody">The <c>requestBody</c> node, or <see langword="null"/>.</param>
    /// <param name="refs">The reference resolver.</param>
    /// <param name="sampler">The schema sampler.</param>
    /// <param name="path">The report path of the operation.</param>
    /// <param name="report">The import report.</param>
    /// <returns>The body and the explicit <c>Content-Type</c> header value when the body kind would not send it.</returns>
    internal static (RequestBody Body, string? ContentTypeHeader) Map(
        DataNode? requestBody, OpenApiRefResolver refs, OpenApiSchemaSampler sampler, string path, ImportReport report)
    {
        var resolved = requestBody is null ? null : refs.Resolve(requestBody, path);
        if (resolved is not { IsObject: true } || resolved["content"] is not { IsObject: true, Members.Count: > 0 } content)
        {
            return (RequestBody.None, null);
        }

        var chosen = Choose(content.Members.Select(m => m.Key).ToList());
        var media = refs.Resolve(content[chosen]!, path);
        var mime = chosen.Split(';')[0].Trim().ToLowerInvariant();
        var schema = refs.Get(media, "schema", path);
        var sample = SampleOf(media, schema, refs, sampler, path);
        if (content.Members.Count > 1)
        {
            report.Info(path, Strings.ImportOpenapiOtherMediaTypes(path, content.Members.Count - 1));
        }

        var body = BodyFor(mime, sample, schema, refs, path, report);
        return (body, SelfTyped.Contains(mime) ? null : chosen.Split(';')[0].Trim());
    }

    private static string Choose(List<string> keys)
    {
        static string Base(string key) => key.Split(';')[0].Trim().ToLowerInvariant();
        Func<string, bool>[] rules =
        [
            k => k == "application/json",
            k => k.EndsWith("+json", StringComparison.Ordinal),
            k => k == "application/x-www-form-urlencoded",
            k => k == "multipart/form-data",
            k => k == "application/xml" || k.EndsWith("+xml", StringComparison.Ordinal),
            k => k.StartsWith("text/", StringComparison.Ordinal),
        ];
        foreach (var rule in rules)
        {
            if (keys.FirstOrDefault(k => rule(Base(k))) is { } match)
            {
                return match;
            }
        }

        return keys[0];
    }

    private static DataNode SampleOf(DataNode media, DataNode? schema, OpenApiRefResolver refs, OpenApiSchemaSampler sampler, string path)
    {
        if (media["example"] is { } example)
        {
            return example;
        }

        if (media["examples"] is { IsObject: true, Members.Count: > 0 } examples
            && refs.Resolve(examples.Members[0].Value, path)["value"] is { } value)
        {
            return value;
        }

        return schema is null ? DataNode.Null : sampler.Sample(schema, path);
    }

    private static RequestBody BodyFor(string mime, DataNode sample, DataNode? schema, OpenApiRefResolver refs, string path, ImportReport report)
    {
        if (mime == "application/json" || mime.EndsWith("+json", StringComparison.Ordinal))
        {
            return RequestBody.Raw(RawContentType.Json, sample.IsString ? sample.AsString()! : DataNodeJson.Write(sample, indented: true));
        }

        if (mime is "application/xml" or "text/xml" || mime.EndsWith("+xml", StringComparison.Ordinal))
        {
            if (sample.IsString)
            {
                return RequestBody.Raw(RawContentType.Xml, sample.AsString()!);
            }

            report.Info(path, Strings.ImportOpenapiXmlSample(path));
            return RequestBody.Raw(RawContentType.Xml, "<!-- example body not generated for XML schemas -->");
        }

        if (mime == "application/x-www-form-urlencoded")
        {
            return RequestBody.UrlEncoded(Fields(sample, schema, refs, path, report, files: false));
        }

        if (mime == "multipart/form-data")
        {
            return RequestBody.FormData(Fields(sample, schema, refs, path, report, files: true));
        }

        if (mime.StartsWith("text/", StringComparison.Ordinal) || mime is "application/javascript")
        {
            var type = mime switch
            {
                "text/html" => RawContentType.Html,
                "text/javascript" or "application/javascript" => RawContentType.JavaScript,
                _ => RawContentType.Text,
            };
            return RequestBody.Raw(type, sample.IsString ? sample.AsString()! : "");
        }

        report.Warning(path, Strings.ImportOpenapiBinaryBody(path));
        return RequestBody.Binary("");
    }

    private static List<FormField> Fields(DataNode sample, DataNode? schema, OpenApiRefResolver refs, string path, ImportReport report, bool files)
    {
        List<FormField> fields = [];
        var properties = schema?["properties"] is { IsObject: true } p ? p : null;
        IEnumerable<KeyValuePair<string, DataNode?>> source = sample.IsObject
            ? sample.Members.Select(m => new KeyValuePair<string, DataNode?>(m.Key, m.Value))
            : properties?.Members.Select(m => new KeyValuePair<string, DataNode?>(m.Key, null)) ?? [];
        foreach (var (name, value) in source)
        {
            var property = properties?[name] is { } raw ? refs.Resolve(raw, path) : null;
            if (files && property is not null && IsFile(property))
            {
                report.Warning(path, Strings.ImportOpenapiFileField(path, name));
                fields.Add(new FormField(name, "", true, FormFieldKind.File));
                continue;
            }

            var text = value is null || value.IsNull ? "" : value.AsString() ?? DataNodeJson.Write(value, indented: false);
            fields.Add(new FormField(name, text));
        }

        return fields;
    }

    private static bool IsFile(DataNode property) =>
        property.GetString("format") == "binary" || (property.GetString("type") == "string" && property["contentEncoding"] is not null);
}
