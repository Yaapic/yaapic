using Yaapic.Core.Import;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Export.OpenApi;

/// <summary>Writes the <c>requestBody</c> of an OpenAPI operation.</summary>
internal static class OpenApiBodyWriter
{
    private const int MaxDepth = 8;

    /// <summary>Writes the request body.</summary>
    /// <param name="body">The body of the request.</param>
    /// <returns>The <c>requestBody</c> object, or null when the request has no body.</returns>
    internal static DataNode? Write(RequestBody body)
    {
        ArgumentNullException.ThrowIfNull(body);
        var (mediaType, schema, example) = body.Kind switch
        {
            BodyKind.Raw => Raw(body),
            BodyKind.UrlEncoded => ("application/x-www-form-urlencoded", FormSchema(body, false), FormExample(body)),
            BodyKind.FormData => ("multipart/form-data", FormSchema(body, true), null),
            BodyKind.Binary => ("application/octet-stream", Binary(), null),
            _ => ("", null, null),
        };
        if (schema is null) return null;

        List<(string, DataNode)> media = [("schema", schema)];
        if (example is not null) media.Add(("example", example));
        return DataNodeJson.Obj(("content", DataNodeJson.Obj((mediaType, DataNodeJson.Obj([.. media])))));
    }

    /// <summary>Infers a JSON schema from a sample value.</summary>
    /// <param name="node">The sample.</param>
    /// <returns>The schema; values nested deeper than 8 levels give an empty schema.</returns>
    internal static DataNode InferSchema(DataNode node) => Infer(node, 0);

    private static DataNode Infer(DataNode node, int depth)
    {
        if (depth >= MaxDepth) return DataNodeJson.Obj();
        switch (node.Kind)
        {
            case DataKind.Object:
                return DataNodeJson.Obj(
                    ("type", DataNodeJson.Str("object")),
                    ("properties", DataNodeJson.Obj([.. node.Members.Select(m => (m.Key, Infer(m.Value, depth + 1)))])));
            case DataKind.Array:
                return DataNodeJson.Obj(
                    ("type", DataNodeJson.Str("array")),
                    ("items", node.Items.Count == 0 ? DataNodeJson.Obj() : Infer(node.Items[0], depth + 1)));
            case DataKind.Number:
                var text = node.AsString() ?? "";
                return Type(text.AsSpan().IndexOfAny('.', 'e', 'E') < 0 ? "integer" : "number");
            case DataKind.Bool:
                return Type("boolean");
            case DataKind.Null:
                return DataNodeJson.Obj(("nullable", DataNode.Bool(true)));
            default:
                return Type("string");
        }
    }

    private static DataNode Type(string type) => DataNodeJson.Obj(("type", DataNodeJson.Str(type)));

    private static DataNode Binary() =>
        DataNodeJson.Obj(("type", DataNodeJson.Str("string")), ("format", DataNodeJson.Str("binary")));

    private static (string, DataNode?, DataNode?) Raw(RequestBody body)
    {
        var mediaType = body.ContentType switch
        {
            RawContentType.Json => "application/json",
            RawContentType.Xml => "application/xml",
            RawContentType.Html => "text/html",
            RawContentType.JavaScript => "application/javascript",
            _ => "text/plain",
        };
        if (body.ContentType == RawContentType.Json)
        {
            try
            {
                var value = DataNodeJson.Parse(body.Text);
                return (mediaType, InferSchema(value), value);
            }
            catch (FormatException)
            {
                // Variables in unquoted places make the text invalid JSON: keep it as a string.
            }
        }

        return (mediaType, Type("string"), DataNodeJson.Str(body.Text));
    }

    private static DataNode FormSchema(RequestBody body, bool files) => DataNodeJson.Obj(
        ("type", DataNodeJson.Str("object")),
        ("properties", DataNodeJson.Obj([.. body.Fields.Where(f => f.Enabled)
            .Select(f => (f.Key, files && f.Kind == FormFieldKind.File ? Binary() : Type("string")))])));

    private static DataNode FormExample(RequestBody body) =>
        DataNodeJson.Obj([.. body.Fields.Where(f => f.Enabled).Select(f => (f.Key, DataNodeJson.Str(f.Value)))]);
}
