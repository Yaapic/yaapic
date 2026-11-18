using Yaapic.Localization;

namespace Yaapic.Core.Import.OpenApi;

/// <summary>Follows local <c>$ref</c> pointers of an OpenAPI document; nothing is copied, the target node itself is returned.</summary>
/// <param name="root">The document root the pointers start from.</param>
/// <param name="report">Where unresolvable references are reported.</param>
internal sealed class OpenApiRefResolver(DataNode root, ImportReport report)
{
    private const int MaxChain = 20;

    /// <summary>Resolves a node: a <c>$ref</c> object is replaced by its target (chains followed), any other node is returned as is.</summary>
    /// <param name="node">The node to resolve.</param>
    /// <param name="path">The report path of the node.</param>
    /// <returns>The target node, or <see cref="DataNode.Null"/> after a warning.</returns>
    public DataNode Resolve(DataNode node, string path)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        var current = node;
        while (current.IsObject && current["$ref"] is { IsString: true } refNode)
        {
            var reference = refNode.AsString()!;
            if (!reference.StartsWith("#/", StringComparison.Ordinal))
            {
                report.Warning(path, Strings.ImportOpenapiExternalRef(path, reference));
                return DataNode.Null;
            }

            if (seen.Count >= MaxChain || !seen.Add(reference))
            {
                report.Warning(path, Strings.ImportOpenapiRefCycle(path, reference));
                return DataNode.Null;
            }

            if (Lookup(reference) is not { } target)
            {
                report.Warning(path, Strings.ImportOpenapiBrokenRef(path, reference));
                return DataNode.Null;
            }

            current = target;
        }

        return current;
    }

    /// <summary>Reads a member of an object and resolves it.</summary>
    /// <param name="obj">The object.</param>
    /// <param name="key">The member name.</param>
    /// <param name="path">The report path of the member.</param>
    /// <returns>The resolved member, or <see langword="null"/> when it is absent.</returns>
    public DataNode? Get(DataNode obj, string key, string path) => obj[key] is { } value ? Resolve(value, path) : null;

    private DataNode? Lookup(string reference)
    {
        var node = root;
        foreach (var raw in reference[2..].Split('/'))
        {
            var key = Uri.UnescapeDataString(raw.Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal));
            DataNode? next = node.IsArray && int.TryParse(key, out var index) ? node[index] : node[key];
            if (next is null)
            {
                return null;
            }

            node = next;
        }

        return node;
    }
}
