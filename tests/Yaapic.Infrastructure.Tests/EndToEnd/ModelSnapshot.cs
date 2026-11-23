using System.Text;
using Yaapic.Domain.Collections;
using Yaapic.Domain.Http;
using Yaapic.ViewModels.Collections;

namespace Yaapic.Infrastructure.Tests.EndToEnd;

/// <summary>Parts of the model a snapshot leaves out, because an export format cannot carry them.</summary>
[Flags]
internal enum SnapshotIgnore
{
    /// <summary>Compare everything.</summary>
    None = 0,

    /// <summary>Folder variables.</summary>
    Variables = 1,

    /// <summary>Folder and request scripts.</summary>
    Scripts = 2,

    /// <summary>Request authentication.</summary>
    Auth = 4,

    /// <summary>Request bodies.</summary>
    Bodies = 8,

    /// <summary>Request headers.</summary>
    Headers = 16,

    /// <summary>Request URLs and query parameters.</summary>
    Urls = 32,
}

/// <summary>Renders a collection tree to a canonical text without identifiers, so two trees compare with one string equality.</summary>
internal static class ModelSnapshot
{
    /// <summary>Renders a tree; the name of the root is replaced by <c>&lt;root&gt;</c> (a re-import is numbered <c>(2)</c>).</summary>
    public static string Of(CollectionNode root, SnapshotIgnore ignore = SnapshotIgnore.None)
    {
        var sb = new StringBuilder();
        Write(sb, root, 0, ignore, isRoot: true);
        return sb.ToString();
    }

    /// <summary>Renders the top-level folder of a shell with the given name.</summary>
    public static string Of(IEnumerable<CollectionNodeViewModel> roots, string name, SnapshotIgnore ignore = SnapshotIgnore.None) =>
        Of(roots.Single(r => r.Name == name).Node, ignore);

    private static void Write(StringBuilder sb, CollectionNode node, int depth, SnapshotIgnore ignore, bool isRoot)
    {
        var pad = new string(' ', depth * 2);
        if (node.Kind == CollectionNodeKind.Request)
        {
            Request(sb, pad, node.Request!, ignore);
            return;
        }

        var folder = node.Folder!;
        sb.Append(pad).Append("folder ").AppendLine(isRoot ? "<root>" : folder.Name);
        if (!ignore.HasFlag(SnapshotIgnore.Variables))
        {
            foreach (var v in folder.Variables) sb.Append(pad).Append("  var ").AppendLine(Entry(v));
        }

        if (!ignore.HasFlag(SnapshotIgnore.Scripts)) Scripts(sb, pad + "  ", folder.Scripts);
        foreach (var child in node.Children) Write(sb, child, depth + 1, ignore, isRoot: false);
    }

    private static void Request(StringBuilder sb, string pad, HttpRequestDefinition r, SnapshotIgnore ignore)
    {
        sb.Append(pad).Append("request ").Append(r.Name).Append(' ').AppendLine(r.Method.ToString());
        if (!ignore.HasFlag(SnapshotIgnore.Urls))
        {
            sb.Append(pad).Append("  url ").AppendLine(r.Url);
            foreach (var p in r.QueryParams) sb.Append(pad).Append("  param ").AppendLine(Entry(p));
        }

        if (!ignore.HasFlag(SnapshotIgnore.Headers))
        {
            foreach (var h in r.Headers) sb.Append(pad).Append("  header ").AppendLine(Entry(h));
        }

        if (!ignore.HasFlag(SnapshotIgnore.Bodies))
        {
            var b = r.Body;
            sb.Append(pad).Append("  body ").Append(b.Kind).Append(' ').Append(b.ContentType).Append(' ').Append(b.Text).Append(' ').AppendLine(b.FilePath);
            foreach (var f in b.Fields) sb.Append(pad).Append("    field ").Append(f.Kind).Append(' ').Append(f.Enabled).Append(' ').Append(f.Key).Append('=').AppendLine(f.Value);
        }

        if (!ignore.HasFlag(SnapshotIgnore.Auth))
        {
            var a = r.Auth;
            sb.Append(pad).Append("  auth ").Append(a.Kind).Append('|').Append(a.Username).Append('|').Append(a.Password).Append('|').Append(a.Token)
                .Append('|').Append(a.Key).Append('|').Append(a.Value).Append('|').AppendLine(a.Location.ToString());
        }

        if (!ignore.HasFlag(SnapshotIgnore.Scripts)) Scripts(sb, pad + "  ", r.Scripts);
    }

    private static void Scripts(StringBuilder sb, string pad, Yaapic.Domain.Scripting.ScriptSet scripts)
    {
        if (!scripts.PreRequest.IsEmpty) sb.Append(pad).Append("pre ").AppendLine(scripts.PreRequest.Source.Trim());
        if (!scripts.PostResponse.IsEmpty) sb.Append(pad).Append("post ").AppendLine(scripts.PostResponse.Source.Trim());
    }

    private static string Entry(KeyValueEntry e) => $"{e.Key}={e.Value} enabled={e.Enabled} secret={e.IsSecret}";
}
