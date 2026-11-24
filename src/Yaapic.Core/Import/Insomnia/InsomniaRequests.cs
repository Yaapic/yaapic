using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.Core.Import.Insomnia;

/// <summary>Walks the flat <c>resources</c> list of an Insomnia export and maps folders, requests and bodies.</summary>
internal sealed class InsomniaRequests
{
    private const int MaxDepth = 32;

    private readonly ImportContext _context;
    private readonly List<DataNode> _resources;
    private readonly string? _workspaceId;
    private readonly HashSet<string> _containerIds = new(StringComparer.Ordinal);
    private int _descriptions;
    private int _scripts;

    internal InsomniaRequests(ImportContext context, IReadOnlyList<DataNode> resources, string? workspaceId)
    {
        _context = context;
        _resources = [.. resources];
        _workspaceId = workspaceId;
        foreach (var r in _resources.Where(r => Type(r) is "workspace" or "request_group"))
        {
            if (r.GetString("_id") is { } id)
            {
                _containerIds.Add(id);
            }
        }
    }

    /// <summary>Reads the items placed directly under the workspace, or under an unknown parent.</summary>
    /// <param name="path">The report path of the collection.</param>
    /// <returns>The folders and requests, in resource order.</returns>
    internal List<ImportItem> ReadRoot(string path)
    {
        var roots = _resources.Where(r =>
            Type(r) is "request" or "request_group"
            && (r.GetString("parentId") is not { } parent || parent == _workspaceId || !_containerIds.Contains(parent)))
            .ToList();
        var items = ReadItems(roots, path, 1);
        CountOtherTypes();
        return items;
    }

    /// <summary>Adds the report entries that are counted over the whole export.</summary>
    internal void Finish()
    {
        if (_scripts > 0)
        {
            _context.Report.Warning("", Strings.ImportInsomniaScripts(_scripts));
        }

        if (_descriptions > 0)
        {
            _context.Report.Info("", Strings.ImportDescriptionsDropped(_descriptions));
        }
    }

    private static string Type(DataNode resource) => resource.GetString("_type") ?? "";

    private string T(string? text, string path) => InsomniaTemplates.Translate(text ?? "", path, _context.Report);

    private List<KeyValueEntry> Entries(DataNode? node, string path) =>
        [.. (node?.Items ?? [])
            .Select(h => new KeyValueEntry(T(h.GetString("name"), path), T(h.GetString("value"), path), h.GetBool("disabled") != true))
            .Where(e => e.Key.Length > 0 || e.Value.Length > 0)];

    private void CountOtherTypes()
    {
        var unsupported = _resources.Count(r => Type(r) is "grpc_request" or "websocket_request");
        if (unsupported > 0)
        {
            _context.Report.Skipped("", Strings.ImportInsomniaUnsupportedTypes(unsupported));
        }

        var ignored = _resources.Count(r => Type(r) is "cookie_jar" or "api_spec" || Type(r).StartsWith("unit_test", StringComparison.Ordinal));
        if (ignored > 0)
        {
            _context.Report.Info("", Strings.ImportInsomniaIgnoredTypes(ignored));
        }
    }

    private List<ImportItem> ReadItems(List<DataNode> nodes, string path, int depth)
    {
        if (nodes.Count > 1)
        {
            _context.Report.AddOnce(ImportSeverity.Info, path, Strings.ImportOrderLost);
        }

        List<ImportItem> result = [];
        foreach (var node in nodes)
        {
            var name = node.GetString("name") ?? "";
            var itemPath = ImportPath.Join(path, name);
            if (Type(node) == "request_group")
            {
                if (depth >= MaxDepth)
                {
                    _context.Report.Skipped(itemPath, Strings.ImportPostmanItemUnknown(itemPath));
                    continue;
                }

                var id = node.GetString("_id");
                var children = _resources.Where(r =>
                    Type(r) is "request" or "request_group" && id is not null && r.GetString("parentId") == id).ToList();
                var folder = new FolderDefinition(
                    _context.NewId(), ImportHelpers.SafeName(name, "Folder"), node.GetString("description") ?? "", [], ScriptSet.Empty);
                result.Add(new ImportFolder(folder, ReadItems(children, itemPath, depth + 1)));
            }
            else if (ReadRequest(node, itemPath) is { } request)
            {
                result.Add(new ImportRequest(request));
            }
        }

        return result;
    }

    private HttpRequestDefinition? ReadRequest(DataNode node, string path)
    {
        var methodText = node.GetString("method") ?? "GET";
        if (!ImportHelpers.TryParseMethod(methodText, out var method))
        {
            _context.Report.Skipped(path, Strings.ImportMethodUnsupported(methodText));
            return null;
        }

        var (url, query) = ImportHelpers.SplitUrl(T(node.GetString("url"), path));
        List<KeyValueEntry> entries = [.. query, .. Entries(node["parameters"], path)];
        var headers = Entries(node["headers"], path);
        var body = ReadBody(node["body"], path, headers);

        if (!string.IsNullOrEmpty(node.GetString("description")))
        {
            _descriptions++;
        }

        _scripts += (string.IsNullOrWhiteSpace(node.GetString("preRequestScript")) ? 0 : 1)
            + (string.IsNullOrWhiteSpace(node.GetString("afterResponseScript")) ? 0 : 1);

        var settings = node.GetString("settingFollowRedirects") == "off" ? new RequestSettings(false, 0) : RequestSettings.Default;
        return new HttpRequestDefinition(
            _context.NewId(),
            ImportHelpers.SafeName(node.GetString("name"), ImportHelpers.DescribeRequest(method, url)),
            method,
            url,
            entries,
            headers,
            body,
            InsomniaAuth.Map(node["authentication"], path, _context.Report),
            settings);
    }

    private RequestBody ReadBody(DataNode? body, string path, List<KeyValueEntry> headers)
    {
        if (body is not { IsObject: true } || body.Members.Count == 0)
        {
            return RequestBody.None;
        }

        var mimeText = body.GetString("mimeType") ?? "";
        var mime = mimeText.Split(';')[0].Trim().ToLowerInvariant();
        var text = body.GetString("text") is { } raw ? T(raw, path) : null;
        switch (mime)
        {
            case "application/x-www-form-urlencoded":
                return RequestBody.UrlEncoded([.. Entries(body["params"], path).Select(e => new FormField(e.Key, e.Value, e.Enabled))]);
            case "multipart/form-data":
                return RequestBody.FormData(FormData(body["params"], path));
            case "application/octet-stream" when body.GetString("fileName") is { Length: > 0 } file:
                _context.Report.Warning(path, Strings.ImportFileReference(path, file));
                return RequestBody.Binary(file);
        }

        if (text is null)
        {
            return RequestBody.None;
        }

        switch (mime)
        {
            case "application/json" or "application/xml" or "text/xml" or "text/html" or "text/plain" or "application/javascript":
                return RequestBody.Raw(ImportHelpers.ContentTypeFromMime(mime), text);
            case "application/graphql":
                return RequestBody.Raw(RawContentType.Json, text);
            case "":
                return RequestBody.Raw(RawContentType.Text, text);
            default:
                if (!headers.Exists(h => h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase)))
                {
                    headers.Add(new KeyValueEntry("Content-Type", mimeText.Trim()));
                }

                return RequestBody.Raw(RawContentType.Text, text);
        }
    }

    private List<FormField> FormData(DataNode? parameters, string path)
    {
        List<FormField> fields = [];
        foreach (var p in parameters?.Items ?? [])
        {
            var key = T(p.GetString("name"), path);
            var enabled = p.GetBool("disabled") != true;
            if (p.GetString("type") == "file")
            {
                var file = p.GetString("fileName") ?? "";
                _context.Report.Warning(path, Strings.ImportFileReference(path, file));
                fields.Add(new FormField(key, file, enabled, FormFieldKind.File));
            }
            else
            {
                fields.Add(new FormField(key, T(p.GetString("value"), path), enabled));
            }
        }

        return fields;
    }
}
