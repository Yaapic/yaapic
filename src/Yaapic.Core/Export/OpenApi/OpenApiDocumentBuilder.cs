using System.Text.RegularExpressions;
using Yaapic.Core.Import;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Core.Export.OpenApi;

/// <summary>Infers an OpenAPI 3.0.3 document from the requests of a collection.</summary>
internal static partial class OpenApiDocumentBuilder
{
    private static readonly string[] SkippedHeaders = ["Content-Type", "Accept", "Authorization"];

    /// <summary>Builds the document: <c>openapi</c>, <c>info</c>, <c>servers</c>, <c>tags</c> and <c>paths</c>.</summary>
    /// <param name="source">The export source; its collection is required.</param>
    /// <param name="report">Receives the warnings and the inference note.</param>
    /// <returns>The document as an object node, members in OpenAPI order.</returns>
    /// <exception cref="ExportException">The source has no collection.</exception>
    internal static DataNode Build(ExportSource source, ImportReport report)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(report);
        var root = source.Collection ?? throw new ExportException(Strings.ExportCollectionNotFound);
        var variables = root.Folder!.Variables.Where(v => v.Enabled && v.Value.Length > 0)
            .GroupBy(v => v.Key).ToDictionary(g => g.Key, g => g.First().Value);

        List<string> origins = [];
        List<(string Name, string Description)> tags = [];
        List<(string Path, List<(string Method, DataNode Operation)> Operations)> paths = [];
        HashSet<string> operationIds = [];

        Walk(root.Children, [], "");

        report.Info("", Strings.ExportOpenApiInferred);
        List<(string, DataNode)> info = [("title", DataNodeJson.Str(root.Name)), ("version", DataNodeJson.Str("1.0.0"))];
        if (root.Folder.Description.Length > 0) info.Add(("description", DataNodeJson.Str(root.Folder.Description)));

        List<(string, DataNode)> document = [("openapi", DataNodeJson.Str("3.0.3")), ("info", DataNodeJson.Obj([.. info]))];
        if (origins.Count > 0)
        {
            document.Add(("servers", DataNodeJson.Arr([.. origins.Select(o => DataNodeJson.Obj(("url", DataNodeJson.Str(ServerUrl(o)))))])));
        }

        if (tags.Count > 0)
        {
            document.Add(("tags", DataNodeJson.Arr([.. tags.Select(t => t.Description.Length == 0
                ? DataNodeJson.Obj(("name", DataNodeJson.Str(t.Name)))
                : DataNodeJson.Obj(("name", DataNodeJson.Str(t.Name)), ("description", DataNodeJson.Str(t.Description))))])));
        }

        document.Add(("paths", DataNodeJson.Obj([.. paths.Select(p => (p.Path, DataNodeJson.Obj([.. p.Operations.Select(o => (o.Method, o.Operation))])))])));
        return DataNodeJson.Obj([.. document]);

        string ServerUrl(string origin)
        {
            if (!origin.StartsWith("{{", StringComparison.Ordinal)) return origin;
            var name = origin[2..^2].Trim();
            if (variables.TryGetValue(name, out var value)) return value.TrimEnd('/');
            report.AddOnce(ImportSeverity.Warning, "", Strings.ExportOpenApiUnresolvedServer(name));
            return "/";
        }

        void Walk(IReadOnlyList<CollectionNode> nodes, List<CollectionNode> folders, string location)
        {
            foreach (var node in nodes)
            {
                var nodeLocation = ImportPath.Join(location, node.Name);
                if (node.Kind == CollectionNodeKind.Folder) Walk(node.Children, [.. folders, node], nodeLocation);
                else AddOperation(node.Request!, folders, nodeLocation);
            }
        }

        void AddOperation(HttpRequestDefinition request, List<CollectionNode> folders, string location)
        {
            var (origin, path) = Split(request.Url);
            var method = request.Method.ToText().ToLowerInvariant();
            var index = paths.FindIndex(p => p.Path == path);
            if (index >= 0 && paths[index].Operations.Exists(o => o.Method == method))
            {
                report.Warning(location, Strings.ExportOpenApiDuplicateOperation(method.ToUpperInvariant(), path));
                return;
            }

            if (origin.Length > 0 && !origins.Contains(origin)) origins.Add(origin);

            List<(string, DataNode)> operation = [("summary", DataNodeJson.Str(request.Name)), ("operationId", DataNodeJson.Str(OperationId(request.Name)))];
            if (folders.Count > 0)
            {
                var tag = string.Join(" / ", folders.Select(f => f.Name));
                if (!tags.Exists(t => t.Name == tag)) tags.Add((tag, folders[^1].Folder!.Description));
                operation.Add(("tags", DataNodeJson.Arr(DataNodeJson.Str(tag))));
            }

            var parameters = Parameters(request, path);
            if (parameters.Length > 0) operation.Add(("parameters", DataNodeJson.Arr(parameters)));
            operation.Add(("responses", DataNodeJson.Obj(("default", DataNodeJson.Obj(("description", DataNodeJson.Str("Response")))))));

            if (index < 0)
            {
                paths.Add((path, []));
                index = paths.Count - 1;
            }

            paths[index].Operations.Add((method, DataNodeJson.Obj([.. operation])));
        }

        string OperationId(string name)
        {
            var words = NonAlphanumeric().Split(name).Where(w => w.Length > 0).ToArray();
            var id = words.Length == 0
                ? "operation"
                : words[0].ToLowerInvariant() + string.Concat(words.Skip(1).Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
            var candidate = id;
            for (var n = 2; !operationIds.Add(candidate); n++) candidate = id + n;
            return candidate;
        }

        DataNode[] Parameters(HttpRequestDefinition request, string path)
        {
            List<DataNode> result = [];
            foreach (var name in PathParameter().Matches(path).Select(m => m.Groups[1].Value).Distinct())
            {
                result.Add(Parameter(name, "path", true, variables.GetValueOrDefault(name, "")));
            }

            result.AddRange(request.QueryParams.Select(q => Parameter(q.Key, "query", q.Enabled, q.Value)));
            result.AddRange(request.Headers
                .Where(h => !SkippedHeaders.Contains(h.Key, StringComparer.OrdinalIgnoreCase))
                .Select(h => Parameter(h.Key, "header", h.Enabled, h.Value)));
            return [.. result];
        }
    }

    private static DataNode Parameter(string name, string location, bool required, string example)
    {
        List<(string, DataNode)> members =
        [
            ("name", DataNodeJson.Str(name)),
            ("in", DataNodeJson.Str(location)),
            ("required", DataNode.Bool(required)),
            ("schema", DataNodeJson.Obj(("type", DataNodeJson.Str("string")))),
        ];
        if (example.Length > 0) members.Add(("example", DataNodeJson.Str(example)));
        return DataNodeJson.Obj([.. members]);
    }

    private static (string Origin, string Path) Split(string url)
    {
        var origin = "";
        var scheme = Scheme().Match(url);
        if (scheme.Success)
        {
            origin = scheme.Value;
        }
        else if (url.StartsWith("{{", StringComparison.Ordinal) && url.IndexOf("}}", StringComparison.Ordinal) is var end and > 0
                 && (end + 2 == url.Length || url[end + 2] is '/' or '?' or '#'))
        {
            origin = url[..(end + 2)];
        }

        var rest = url[origin.Length..];
        var cut = rest.IndexOfAny(['?', '#']);
        var path = Variable().Replace(cut >= 0 ? rest[..cut] : rest, "{$1}");
        path = string.Join('/', path.Split('/').Select(s => Colon().IsMatch(s) ? "{" + s[1..] + "}" : s));
        return (origin, path.StartsWith('/') ? path : "/" + path);
    }

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.\-]*://[^/?#]*")]
    private static partial Regex Scheme();

    [GeneratedRegex(@"\{\{\s*([^{}]*?)\s*\}\}")]
    private static partial Regex Variable();

    [GeneratedRegex(@"^:[A-Za-z0-9_\-]+$")]
    private static partial Regex Colon();

    [GeneratedRegex(@"\{([^{}/]+)\}")]
    private static partial Regex PathParameter();

    [GeneratedRegex("[^A-Za-z0-9]+")]
    private static partial Regex NonAlphanumeric();
}
