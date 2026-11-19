using Yaapic.Localization;
using static Yaapic.Core.Import.DataNodeJson;

namespace Yaapic.Core.Import.OpenApi;

/// <summary>Rewrites a Swagger 2.0 document as an equivalent OpenAPI 3.0 tree, so only one importer path exists.</summary>
internal static class SwaggerNormalizer
{
    private const string DefinitionsRef = "#/definitions/";
    private const string ParametersRef = "#/parameters/";
    private static readonly string[] Methods = ["get", "put", "post", "delete", "options", "head", "patch", "trace"];
    private static readonly string[] DroppedOperationKeys = ["parameters", "consumes", "produces", "responses", "schemes"];
    private static readonly string[] SchemaKeys = ["type", "format", "enum", "default", "items"];
    private static readonly string[] FormKeys = [.. SchemaKeys, "description"];
    private static readonly string[] ParameterKeys = ["name", "in", "required", "description"];
    private static readonly string[] OAuthKeys = ["authorizationUrl", "tokenUrl", "scopes"];

    /// <summary>Converts a Swagger 2.0 document.</summary>
    /// <param name="swagger">The Swagger 2.0 root.</param>
    /// <param name="report">Receives an information entry about the conversion.</param>
    /// <returns>The OpenAPI 3.0 root.</returns>
    public static DataNode ToOpenApi3(DataNode swagger, ImportReport report)
    {
        report.Info(string.Empty, Strings.ImportOpenapiSwaggerConverted);

        List<KeyValuePair<string, DataNode>> root = [new("openapi", Str("3.0.3"))];
        if (swagger["info"] is { } info)
        {
            root.Add(new("info", Rewrite(info)));
        }

        if (Servers(swagger) is { } servers)
        {
            root.Add(new("servers", servers));
        }

        if (swagger["tags"] is { } tags)
        {
            root.Add(new("tags", Rewrite(tags)));
        }

        var paths = swagger["paths"]!.Members
            .Select(m => new KeyValuePair<string, DataNode>(m.Key, m.Value.IsObject ? PathItem(swagger, m.Value) : m.Value));
        root.Add(new("paths", DataNode.Object([.. paths])));

        List<KeyValuePair<string, DataNode>> components = [];
        if (swagger["definitions"] is { IsObject: true } definitions)
        {
            components.Add(new("schemas", Rewrite(definitions)));
        }

        if (swagger["securityDefinitions"] is { IsObject: true } schemes)
        {
            components.Add(new("securitySchemes", DataNode.Object([.. schemes.Members.Select(m => new KeyValuePair<string, DataNode>(m.Key, Scheme(m.Value)))])));
        }

        if (components.Count > 0)
        {
            root.Add(new("components", DataNode.Object(components)));
        }

        if (swagger["security"] is { } security)
        {
            root.Add(new("security", Rewrite(security)));
        }

        return DataNode.Object(root);
    }

    private static DataNode? Servers(DataNode swagger)
    {
        var host = swagger.GetString("host");
        var basePath = (swagger.GetString("basePath") ?? string.Empty).TrimEnd('/');
        if (string.IsNullOrEmpty(host))
        {
            return basePath.Length == 0 ? null : Arr(Obj(("url", Str(basePath))));
        }

        var schemes = swagger["schemes"] is { IsArray: true } list
            ? list.Items.Select(s => s.AsString()).OfType<string>().ToArray()
            : ["https"];
        return schemes.Length == 0 ? null : Arr([.. schemes.Select(s => Obj(("url", Str($"{s}://{host}{basePath}"))))]);
    }

    private static DataNode PathItem(DataNode swagger, DataNode item)
    {
        List<KeyValuePair<string, DataNode>> members = [];
        foreach (var (key, value) in item.Members)
        {
            if (key == "parameters")
            {
                // shortcut: body/formData parameters declared at path-item level are dropped, move them to operations when seen in the wild.
                members.Add(new(key, Arr([.. Resolve(swagger, value).Where(IsPlain).Select(Parameter)])));
            }
            else if (Methods.Contains(key, StringComparer.Ordinal) && value.IsObject)
            {
                members.Add(new(key, Operation(swagger, value)));
            }
            else
            {
                members.Add(new(key, Rewrite(value)));
            }
        }

        return DataNode.Object(members);
    }

    private static DataNode Operation(DataNode swagger, DataNode operation)
    {
        List<KeyValuePair<string, DataNode>> members =
        [
            .. operation.Members
                .Where(m => !DroppedOperationKeys.Contains(m.Key, StringComparer.Ordinal))
                .Select(m => new KeyValuePair<string, DataNode>(m.Key, Rewrite(m.Value))),
        ];

        var parameters = operation["parameters"] is { } raw ? Resolve(swagger, raw) : [];
        var plain = parameters.Where(IsPlain).Select(Parameter).ToArray();
        if (plain.Length > 0)
        {
            members.Add(new("parameters", Arr(plain)));
        }

        var consumes = Consumes(operation) ?? Consumes(swagger);
        var form = parameters.Where(p => p.GetString("in") == "formData").ToArray();
        if (parameters.Find(p => p.GetString("in") == "body") is { } body)
        {
            members.Add(new("requestBody", Body(body, consumes ?? ["application/json"])));
        }
        else if (form.Length > 0)
        {
            members.Add(new("requestBody", Form(form, consumes)));
        }

        return DataNode.Object(members);
    }

    private static bool IsPlain(DataNode parameter) => parameter.GetString("in") is not ("body" or "formData");

    private static string[]? Consumes(DataNode node) =>
        node["consumes"] is { IsArray: true } list ? [.. list.Items.Select(i => i.AsString()).OfType<string>()] : null;

    private static List<DataNode> Resolve(DataNode swagger, DataNode parameters)
    {
        List<DataNode> result = [];
        foreach (var parameter in parameters.Items)
        {
            var reference = parameter.GetString("$ref");
            var target = reference is not null && reference.StartsWith(ParametersRef, StringComparison.Ordinal)
                ? swagger["parameters"]?[Uri.UnescapeDataString(reference[ParametersRef.Length..])]
                : parameter;
            if (target is { IsObject: true } && target.GetString("in") is not null)
            {
                result.Add(target);
            }
        }

        return result;
    }

    private static DataNode Parameter(DataNode parameter)
    {
        List<KeyValuePair<string, DataNode>> members =
        [
            .. ParameterKeys.Where(k => parameter[k] is not null).Select(k => new KeyValuePair<string, DataNode>(k, parameter[k]!)),
            new("schema", Pick(parameter, SchemaKeys)),
        ];
        return DataNode.Object(members);
    }

    private static DataNode Pick(DataNode source, string[] keys) =>
        DataNode.Object([.. keys.Where(k => source[k] is not null).Select(k => new KeyValuePair<string, DataNode>(k, Rewrite(source[k]!)))]);

    private static DataNode Body(DataNode body, string[] mediaTypes)
    {
        var schema = body["schema"] is { } s ? Rewrite(s) : Obj();
        List<KeyValuePair<string, DataNode>> members =
        [
            new("content", DataNode.Object([.. mediaTypes.Select(m => new KeyValuePair<string, DataNode>(m, Obj(("schema", schema))))])),
        ];
        if (body["required"] is { } required)
        {
            members.Add(new("required", required));
        }

        return DataNode.Object(members);
    }

    private static DataNode Form(DataNode[] fields, string[]? consumes)
    {
        var multipart = fields.Any(f => f.GetString("type") == "file")
            || (consumes?.Contains("multipart/form-data", StringComparer.OrdinalIgnoreCase) ?? false);
        var properties = fields.Select(f => new KeyValuePair<string, DataNode>(
            f.GetString("name") ?? string.Empty,
            f.GetString("type") == "file"
                ? Obj(("type", Str("string")), ("format", Str("binary")))
                : Pick(f, FormKeys)));
        var schema = Obj(("type", Str("object")), ("properties", DataNode.Object([.. properties])));
        return Obj(("content", Obj((multipart ? "multipart/form-data" : "application/x-www-form-urlencoded", Obj(("schema", schema))))));
    }

    private static DataNode Scheme(DataNode scheme)
    {
        switch (scheme.GetString("type"))
        {
            case "basic":
                return Obj(("type", Str("http")), ("scheme", Str("basic")));
            case "oauth2":
                var flow = scheme.GetString("flow") switch
                {
                    "application" => "clientCredentials",
                    "password" => "password",
                    "accessCode" => "authorizationCode",
                    "implicit" => "implicit",
                    _ => null,
                };
                var fields = OAuthKeys
                    .Where(k => scheme[k] is not null
                        && (k != "authorizationUrl" || flow is "authorizationCode" or "implicit")
                        && (k != "tokenUrl" || flow != "implicit"))
                    .Select(k => new KeyValuePair<string, DataNode>(k, scheme[k]!));
                return Obj(("type", Str("oauth2")), ("flows", flow is null ? Obj() : Obj((flow, DataNode.Object([.. fields])))));
            default:
                return Rewrite(scheme);
        }
    }

    // Recursion is bounded by the parser's depth limit.
    private static DataNode Rewrite(DataNode node) => node.Kind switch
    {
        DataKind.Object => DataNode.Object([.. node.Members.Select(m => new KeyValuePair<string, DataNode>(m.Key, Rewrite(m.Key, m.Value)))]),
        DataKind.Array => DataNode.Array([.. node.Items.Select(Rewrite)]),
        _ => node,
    };

    private static DataNode Rewrite(string key, DataNode value) =>
        key == "$ref" && value.AsString() is { } text && text.StartsWith(DefinitionsRef, StringComparison.Ordinal)
            ? Str("#/components/schemas/" + text[DefinitionsRef.Length..])
            : Rewrite(value);
}
