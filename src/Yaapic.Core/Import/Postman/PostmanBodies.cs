using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Import.Postman;

/// <summary>Maps the <c>request.body</c> of a Postman item.</summary>
internal static class PostmanBodies
{
    /// <summary>Maps a Postman body.</summary>
    /// <param name="body">The <c>body</c> node, possibly null.</param>
    /// <param name="path">The report path of the request.</param>
    /// <param name="report">Receives the warnings and infos.</param>
    /// <param name="contentTypeHeader">The value of the request <c>Content-Type</c> header, or null when it has none.</param>
    /// <returns>The body, and the <c>Content-Type</c> the caller must add when the request has none (GraphQL), or null.</returns>
    internal static (RequestBody Body, string? AddedContentType) Map(
        DataNode? body, string path, ImportReport report, string? contentTypeHeader)
    {
        if (body is not { IsObject: true } || body.GetString("mode") is not { Length: > 0 } mode)
        {
            return (RequestBody.None, null);
        }

        switch (mode)
        {
            case "raw":
                return (Raw(body, path, report, contentTypeHeader), null);
            case "urlencoded":
                return (RequestBody.UrlEncoded(UrlEncoded(body["urlencoded"], path, report)), null);
            case "formdata":
                return (RequestBody.FormData(FormData(body["formdata"], path, report)), null);
            case "file":
                return (Binary(body["file"]?.GetString("src"), path, report), null);
            case "graphql":
                return GraphQl(body["graphql"], path, report, contentTypeHeader);
            default:
                report.Warning(path, Strings.ImportPostmanBodyModeUnsupported(path, mode));
                return (RequestBody.None, null);
        }
    }

    private static RequestBody Raw(DataNode body, string path, ImportReport report, string? contentTypeHeader)
    {
        var text = ImportHelpers.TranslateDynamicVariables(body.GetString("raw") ?? "", path, report);
        var type = body["options"]?["raw"] is { IsObject: true } options
            ? options.GetString("language") switch
            {
                "json" => RawContentType.Json,
                "xml" => RawContentType.Xml,
                "html" => RawContentType.Html,
                "javascript" => RawContentType.JavaScript,
                _ => RawContentType.Text,
            }
            : ImportHelpers.ContentTypeFromMime(contentTypeHeader);
        return RequestBody.Raw(type, text);
    }

    private static List<FormField> UrlEncoded(DataNode? node, string path, ImportReport report)
    {
        List<FormField> fields = [];
        foreach (var f in node?.Items ?? [])
        {
            if (f.GetString("key") is { Length: > 0 } key)
            {
                var value = ImportHelpers.TranslateDynamicVariables(f.GetString("value") ?? "", path, report);
                fields.Add(new FormField(key, value, f.GetBool("disabled") != true));
            }
        }

        return fields;
    }

    private static List<FormField> FormData(DataNode? node, string path, ImportReport report)
    {
        List<FormField> fields = [];
        foreach (var f in node?.Items ?? [])
        {
            if (f.GetString("key") is not { Length: > 0 } key)
            {
                continue;
            }

            var enabled = f.GetBool("disabled") != true;
            if (f.GetString("type") == "file")
            {
                var src = f["src"] is { IsArray: true } list ? list.Items.Select(i => i.AsString()).FirstOrDefault(s => s is not null) : f.GetString("src");
                if (string.IsNullOrEmpty(src))
                {
                    report.Warning(path, Strings.ImportPostmanFileMissing(path, key));
                    continue;
                }

                report.Warning(path, Strings.ImportFileReference(path, src));
                fields.Add(new FormField(key, src, enabled, FormFieldKind.File));
            }
            else
            {
                var value = ImportHelpers.TranslateDynamicVariables(f.GetString("value") ?? "", path, report);
                fields.Add(new FormField(key, value, enabled));
            }
        }

        return fields;
    }

    private static RequestBody Binary(string? src, string path, ImportReport report)
    {
        if (string.IsNullOrEmpty(src))
        {
            report.Warning(path, Strings.ImportPostmanFileMissing(path, ""));
            return RequestBody.None;
        }

        report.Warning(path, Strings.ImportFileReference(path, src));
        return RequestBody.Binary(src);
    }

    private static (RequestBody Body, string? AddedContentType) GraphQl(
        DataNode? graphql, string path, ImportReport report, string? contentTypeHeader)
    {
        var variables = graphql?["variables"];
        DataNode parsed = DataNodeJson.Obj();
        if (variables is { IsObject: true })
        {
            parsed = variables;
        }
        else if (variables?.AsString() is { } text && !string.IsNullOrWhiteSpace(text))
        {
            try
            {
                if (DataNodeJson.Parse(text) is { IsObject: true } node)
                {
                    parsed = node;
                }
            }
            catch (FormatException)
            {
                // Invalid variables are replaced by {}.
            }
        }

        var json = DataNodeJson.Write(
            DataNodeJson.Obj(("query", DataNodeJson.Str(graphql?.GetString("query") ?? "")), ("variables", parsed)),
            indented: false);
        report.Info(path, Strings.ImportPostmanGraphQl);
        return (RequestBody.Raw(RawContentType.Json, json), contentTypeHeader is null ? "application/json" : null);
    }
}
