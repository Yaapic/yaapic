using System.Text;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.Core.Import.OpenApi;

/// <summary>Turns the servers and path operations of an OpenAPI document into a collection tree.</summary>
/// <param name="root">The document root.</param>
/// <param name="refs">The reference resolver of the document.</param>
/// <param name="context">The import context.</param>
internal sealed class OpenApiOperations(DataNode root, OpenApiRefResolver refs, ImportContext context)
{
    private static readonly string[] Methods = ["get", "put", "post", "delete", "options", "head", "patch"];
    private static readonly string[] PathItemKeys = ["parameters", "summary", "description", "servers", "$ref"];
    private static readonly string[] IgnoredHeaders = ["Accept", "Content-Type", "Authorization"];

    private readonly List<KeyValueEntry> _variables = [];
    private readonly List<ImportItem> _children = [];
    private readonly Dictionary<string, List<ImportItem>> _folders = new(StringComparer.Ordinal);
    private int _cookies;
    private int _descriptions;

    /// <summary>Builds the collection folder with its folders, requests and variables, plus the scoped environments.</summary>
    /// <param name="name">The collection name.</param>
    /// <param name="description">The collection description.</param>
    /// <returns>The collection tree and the environments of the further servers.</returns>
    internal (ImportFolder Collection, List<EnvironmentDefinition> Environments) Build(string name, string description)
    {
        var collectionId = context.NewId();
        var environments = ReadServers(collectionId);
        if (root["paths"] is { IsObject: true } paths)
        {
            foreach (var (path, value) in paths.Members)
            {
                ReadPathItem(path, value);
            }
        }

        if (_cookies > 0)
        {
            context.Report.Info("", Strings.ImportOpenapiCookiesIgnored(_cookies));
        }

        if (_descriptions > 0)
        {
            context.Report.Info("", Strings.ImportDescriptionsDropped(_descriptions));
        }

        var folder = new FolderDefinition(collectionId, name, description, _variables, ScriptSet.Empty);
        return (new ImportFolder(folder, _children), environments);
    }

    private static string SampleValue(DataNode parameter, DataNode? schema, string examplePath, OpenApiRefResolver refs)
    {
        var node = parameter["example"];
        if (node is null && parameter["examples"] is { IsObject: true, Members.Count: > 0 } examples)
        {
            node = refs.Resolve(examples.Members[0].Value, examplePath)["value"];
        }

        node ??= schema?["example"] ?? schema?["default"] ?? (schema?["enum"] is { IsArray: true } values ? values[0] : null);
        if (node is null || node.IsNull)
        {
            return "";
        }

        // style form (the query default) writes an array once, comma-joined; any other style keeps compact JSON.
        if (node.IsArray && node.Items.All(i => i.AsString() is not null) && parameter.GetString("style") is null or "form")
        {
            return string.Join(',', node.Items.Select(i => i.AsString()));
        }

        return node.AsString() ?? DataNodeJson.Write(node, false);
    }

    private static string ToVariables(string path)
    {
        StringBuilder sb = new();
        for (var i = 0; i < path.Length; i++)
        {
            var close = path[i] == '{' ? path.IndexOf('}', i) : -1;
            if (close > i)
            {
                sb.Append("{{").Append(path, i + 1, close - i - 1).Append("}}");
                i = close;
            }
            else
            {
                sb.Append(path[i]);
            }
        }

        return sb.ToString();
    }

    private static string? KeyOf(DataNode parameter) =>
        parameter.GetString("in") is { } place && parameter.GetString("name") is { } name ? place + "\n" + name : null;

    private List<EnvironmentDefinition> ReadServers(Guid collectionId)
    {
        List<EnvironmentDefinition> environments = [];
        List<string> urls = [];
        if (root["servers"] is { IsArray: true } servers)
        {
            foreach (var server in servers.Items)
            {
                urls.Add(ServerUrl(server));
                if (urls.Count > 1)
                {
                    environments.Add(new EnvironmentDefinition(
                        context.NewId(),
                        ImportHelpers.SafeName(server.GetString("description"), urls[^1]),
                        [new KeyValueEntry("baseUrl", urls[^1])],
                        collectionId));
                }
            }
        }

        var baseUrl = urls.Count > 0 ? urls[0] : "";
        if (!baseUrl.Contains("://", StringComparison.Ordinal))
        {
            context.Report.AddOnce(ImportSeverity.Info, "", Strings.ImportOpenapiRelativeServer(baseUrl));
        }

        _variables.Add(new KeyValueEntry("baseUrl", baseUrl));
        return environments;
    }

    private string ServerUrl(DataNode server)
    {
        var url = server.GetString("url") ?? "";
        if (server["variables"] is { IsObject: true } variables)
        {
            foreach (var (key, value) in variables.Members)
            {
                if (value.GetString("default") is { } fallback)
                {
                    url = url.Replace("{" + key + "}", fallback, StringComparison.Ordinal);
                }
                else
                {
                    context.Report.Warning("servers", Strings.ImportOpenapiServerVariable(key));
                }
            }
        }

        return url.TrimEnd('/');
    }

    private void ReadPathItem(string path, DataNode value)
    {
        var item = refs.Resolve(value, path);
        if (!item.IsObject)
        {
            return;
        }

        if (item["servers"] is not null)
        {
            context.Report.AddOnce(ImportSeverity.Info, path, Strings.ImportOpenapiLocalServers);
        }

        foreach (var (key, _) in item.Members)
        {
            if (!Methods.Contains(key) && !PathItemKeys.Contains(key) && !key.StartsWith("x-", StringComparison.Ordinal))
            {
                context.Report.Skipped(ImportPath.Join(path, key), Strings.ImportMethodUnsupported(key.ToUpperInvariant()));
            }
        }

        foreach (var key in Methods)
        {
            if (item[key] is { IsObject: true } operation)
            {
                ReadOperation(path, key, item, operation);
            }
        }
    }

    private void ReadOperation(string path, string methodKey, DataNode item, DataNode operation)
    {
        ImportHelpers.TryParseMethod(methodKey, out var method);
        var label = method.ToText() + " " + path;
        if (operation["servers"] is not null)
        {
            context.Report.AddOnce(ImportSeverity.Info, label, Strings.ImportOpenapiLocalServers);
        }

        if (operation.GetBool("deprecated") == true)
        {
            context.Report.Info(label, Strings.ImportOpenapiDeprecated(label));
        }

        if (operation.GetString("description") is { Length: > 0 })
        {
            _descriptions++;
        }

        var (query, headers) = ReadParameters(label, item, operation);
        var request = new HttpRequestDefinition(
            context.NewId(),
            ImportHelpers.SafeName(operation.GetString("summary"), ImportHelpers.SafeName(operation.GetString("operationId"), label)),
            method,
            "{{baseUrl}}" + ToVariables(path),
            query,
            headers,
            RequestBody.None,
            RequestAuth.None,
            RequestSettings.Default,
            ScriptSet.Empty);
        TargetFolder(operation).Add(new ImportRequest(request));
    }

    private List<ImportItem> TargetFolder(DataNode operation)
    {
        if (operation["tags"]?[0]?.AsString() is not { } tag || string.IsNullOrWhiteSpace(tag))
        {
            return _children;
        }

        var name = ImportHelpers.SafeName(tag, tag);
        if (!_folders.TryGetValue(name, out var children))
        {
            var description = root["tags"] is { IsArray: true } tags
                ? tags.Items.FirstOrDefault(t => t.GetString("name") == tag)?.GetString("description") ?? ""
                : "";
            children = [];
            _folders[name] = children;
            _children.Add(new ImportFolder(new FolderDefinition(context.NewId(), name, description, [], ScriptSet.Empty), children));
        }

        return children;
    }

    private (List<KeyValueEntry> Query, List<KeyValueEntry> Headers) ReadParameters(string label, DataNode item, DataNode operation)
    {
        List<DataNode> merged = [];
        foreach (var source in (DataNode[])[item, operation])
        {
            if (source["parameters"] is not { IsArray: true } list)
            {
                continue;
            }

            foreach (var raw in list.Items)
            {
                var parameter = refs.Resolve(raw, label);
                var key = parameter.IsObject ? KeyOf(parameter) : null;
                if (key is null)
                {
                    continue;
                }

                var index = merged.FindIndex(m => KeyOf(m) == key);
                if (index >= 0)
                {
                    merged[index] = parameter;
                }
                else
                {
                    merged.Add(parameter);
                }
            }
        }

        List<KeyValueEntry> query = [];
        List<KeyValueEntry> headers = [];
        foreach (var parameter in merged)
        {
            var name = parameter.GetString("name")!;
            var value = SampleValue(parameter, refs.Get(parameter, "schema", label), label, refs);
            var enabled = parameter.GetBool("required") == true;
            switch (parameter.GetString("in"))
            {
                case "query":
                    query.Add(new KeyValueEntry(name, value, enabled));
                    break;
                case "header" when !IgnoredHeaders.Contains(name, StringComparer.OrdinalIgnoreCase):
                    headers.Add(new KeyValueEntry(name, value, enabled));
                    break;
                case "path" when !_variables.Exists(v => v.Key == name):
                    _variables.Add(new KeyValueEntry(name, value));
                    break;
                case "cookie":
                    _cookies++;
                    break;
            }
        }

        return (query, headers);
    }
}
