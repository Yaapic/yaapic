using System.Text;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.Domain.Scripting;
using Yaapic.Localization;

namespace Yaapic.Core.Import.Postman;

/// <summary>Walks the items of a Postman collection and maps each request.</summary>
internal sealed class PostmanItems(ImportContext context)
{
    private const int MaxDepth = 32;

    private int _examples;
    private int _descriptions;

    /// <summary>Reads a description given as a string or as an object with a <c>content</c> member.</summary>
    /// <param name="node">The description node, possibly null.</param>
    /// <returns>The text, or an empty string.</returns>
    internal static string Text(DataNode? node) =>
        (node is { IsObject: true } ? node.GetString("content") : node?.AsString()) ?? "";

    /// <summary>Reads the items of one level.</summary>
    /// <param name="items">The <c>item</c> array.</param>
    /// <param name="path">The report path of the parent.</param>
    /// <param name="depth">The folder depth, 1 for the collection level.</param>
    /// <returns>The folders and requests, in source order.</returns>
    internal List<ImportItem> ReadItems(DataNode items, string path, int depth)
    {
        List<ImportItem> result = [];
        if (items.Items.Count > 1)
        {
            context.Report.AddOnce(ImportSeverity.Info, path, Strings.ImportOrderLost);
        }

        foreach (var item in items.Items)
        {
            var name = item.GetString("name") ?? "";
            var itemPath = ImportPath.Join(path, name);
            if (item["item"] is { IsArray: true } children)
            {
                if (depth >= MaxDepth)
                {
                    context.Report.Skipped(itemPath, Strings.ImportPostmanItemUnknown(itemPath));
                    continue;
                }

                var folder = new FolderDefinition(context.NewId(), ImportHelpers.SafeName(name, "Folder"), Text(item["description"]), []);
                result.Add(new ImportFolder(folder, ReadItems(children, itemPath, depth + 1)));
            }
            else if (item["request"] is { IsNull: false } request)
            {
                if (ReadRequest(item, request, itemPath) is { } mapped)
                {
                    result.Add(new ImportRequest(mapped));
                }
            }
            else
            {
                context.Report.Skipped(itemPath, Strings.ImportPostmanItemUnknown(itemPath));
            }
        }

        return result;
    }

    /// <summary>Adds the report entries that are counted over the whole collection.</summary>
    internal void Finish()
    {
        if (_examples > 0)
        {
            context.Report.Info("", Strings.ImportPostmanDroppedExamples(_examples));
        }

        if (_descriptions > 0)
        {
            context.Report.Info("", Strings.ImportDescriptionsDropped(_descriptions));
        }
    }

    private HttpRequestDefinition? ReadRequest(DataNode item, DataNode request, string path)
    {
        var methodText = request.IsString ? "GET" : request.GetString("method") ?? "GET";
        if (!ImportHelpers.TryParseMethod(methodText, out var method))
        {
            context.Report.Skipped(path, Strings.ImportMethodUnsupported(methodText));
            return null;
        }

        var (url, query) = ReadUrl(request.IsString ? request : request["url"], path);
        if (item["response"] is { IsArray: true } responses)
        {
            _examples += responses.Items.Count;
        }

        if (Text(request["description"]).Length > 0)
        {
            _descriptions++;
        }

        var headers = ReadHeaders(request["header"], path);
        var contentType = headers.Find(h => h.Enabled && h.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))?.Value;
        var (body, addedContentType) = PostmanBodies.Map(request["body"], path, context.Report, contentType);
        if (addedContentType is not null)
        {
            headers.Add(new KeyValueEntry("Content-Type", addedContentType));
        }

        return new HttpRequestDefinition(
            context.NewId(),
            ImportHelpers.SafeName(item.GetString("name"), ImportHelpers.DescribeRequest(method, url)),
            method,
            url,
            query,
            headers,
            body,
            RequestAuth.None,
            RequestSettings.Default,
            ScriptSet.Empty);
    }

    private (string Url, List<KeyValueEntry> Query) ReadUrl(DataNode? node, string path)
    {
        var raw = node switch
        {
            null => "",
            { IsObject: true } => node.GetString("raw") is { Length: > 0 } r ? r : Compose(node),
            _ => node.AsString() ?? "",
        };

        var (url, query) = ImportHelpers.SplitUrl(raw);
        List<KeyValueEntry> entries = [.. query];
        if (node?["query"] is { IsArray: true } list)
        {
            entries = [];
            foreach (var q in list.Items)
            {
                var key = q.GetString("key");
                var value = q.GetString("value");
                if (key is null && value is null)
                {
                    continue;
                }

                entries.Add(new KeyValueEntry(key ?? "", value ?? "", q.GetBool("disabled") != true));
            }
        }

        Dictionary<string, string> variables = [];
        if (node?["variable"] is { IsArray: true } vars)
        {
            foreach (var v in vars.Items)
            {
                if (v.GetString("key") is { Length: > 0 } key)
                {
                    variables[key] = v.GetString("value") ?? "";
                }
            }
        }

        url = Translate(ImportHelpers.SubstitutePathVariables(url, variables), path);
        return (url, [.. entries.Select(e => e with { Value = Translate(e.Value, path) })]);
    }

    private static string Compose(DataNode url)
    {
        StringBuilder sb = new();
        if (url.GetString("protocol") is { Length: > 0 } protocol)
        {
            sb.Append(protocol).Append("://");
        }

        sb.Append(Join(url["host"], '.'));
        if (url.GetString("port") is { Length: > 0 } port)
        {
            sb.Append(':').Append(port);
        }

        var path = Join(url["path"], '/');
        if (path.Length > 0)
        {
            sb.Append('/').Append(path);
        }

        return sb.ToString();
    }

    private static string Join(DataNode? node, char separator)
    {
        if (node is null)
        {
            return "";
        }

        if (!node.IsArray)
        {
            return node.AsString() ?? "";
        }

        return string.Join(separator, node.Items
            .Select(n => n.IsObject ? n.GetString("value") : n.AsString())
            .Where(s => !string.IsNullOrEmpty(s)));
    }

    private List<KeyValueEntry> ReadHeaders(DataNode? node, string path)
    {
        List<KeyValueEntry> headers = [];
        if (node is { IsString: true })
        {
            foreach (var line in node.AsString()!.Split('\n'))
            {
                var colon = line.IndexOf(':');
                if (colon > 0 && line[..colon].Trim() is { Length: > 0 } name)
                {
                    headers.Add(new(name, Translate(line[(colon + 1)..].Trim(), path)));
                }
            }
        }
        else if (node is { IsArray: true })
        {
            foreach (var h in node.Items)
            {
                if (h.GetString("key") is { Length: > 0 } key)
                {
                    headers.Add(new(key, Translate(h.GetString("value") ?? "", path), h.GetBool("disabled") != true));
                }
            }
        }

        return headers;
    }

    private string Translate(string value, string path) => ImportHelpers.TranslateDynamicVariables(value, path, context.Report);
}
