using Yaapic.Domain.Http;

namespace Yaapic.Core.Diff;

/// <summary>Compares two response snapshots.</summary>
public static class ResponseDiffer
{
    /// <summary>Headers that change on every response and are skipped by default.</summary>
    public static readonly IReadOnlyList<string> VolatileHeaders =
        ["Date", "Age", "X-Request-Id", "X-Amzn-Trace-Id", "CF-Ray", "Server-Timing"];

    /// <summary>Compares the status, headers and body of two snapshots.</summary>
    /// <param name="old">The old snapshot.</param>
    /// <param name="current">The new snapshot.</param>
    /// <param name="options">The comparison options.</param>
    /// <returns>The differences.</returns>
    public static ResponseDiff Compare(ResponseSnapshot old, ResponseSnapshot current, ResponseDiffOptions options)
    {
        ArgumentNullException.ThrowIfNull(old);
        ArgumentNullException.ThrowIfNull(current);
        ArgumentNullException.ThrowIfNull(options);

        var oldHeaders = Group(old.Headers, options.IgnoreVolatileHeaders);
        var newHeaders = Group(current.Headers, options.IgnoreVolatileHeaders);
        var names = new SortedSet<string>(oldHeaders.Keys, StringComparer.OrdinalIgnoreCase);
        names.UnionWith(newHeaders.Keys);

        var headers = new List<HeaderDiff>();
        foreach (var name in names)
        {
            var hadOld = oldHeaders.TryGetValue(name, out var o);
            var hasNew = newHeaders.TryGetValue(name, out var n);
            if (hadOld && hasNew)
            {
                if (o!.Value != n!.Value)
                {
                    headers.Add(new HeaderDiff(n.Name, HeaderDiffKind.Changed, o.Value, n.Value));
                }
            }
            else if (hasNew)
            {
                headers.Add(new HeaderDiff(n!.Name, HeaderDiffKind.Added, null, n.Value));
            }
            else
            {
                headers.Add(new HeaderDiff(o!.Name, HeaderDiffKind.Removed, o.Value, null));
            }
        }

        var body = LineDiff.Compute(
            LineDiff.SplitLines(old.BodyText), LineDiff.SplitLines(current.BodyText), options.IgnoreWhitespace);
        return new ResponseDiff(
            old.StatusCode != current.StatusCode,
            Status(old),
            Status(current),
            headers,
            body,
            LineDiff.Summarize(body));
    }

    private static string Status(ResponseSnapshot s) =>
        string.IsNullOrEmpty(s.ReasonPhrase) ? $"{s.StatusCode}" : $"{s.StatusCode} {s.ReasonPhrase}";

    private static Dictionary<string, (string Name, string Value)> Group(IReadOnlyList<KeyValueEntry> headers, bool skipVolatile)
    {
        var values = new Dictionary<string, (string Name, List<string> Values)>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in headers)
        {
            if (skipVolatile && VolatileHeaders.Contains(h.Key, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!values.TryGetValue(h.Key, out var entry))
            {
                entry = (h.Key, []);
                values[h.Key] = entry;
            }

            entry.Values.Add(h.Value);
        }

        return values.ToDictionary(p => p.Key, p => (p.Value.Name, string.Join(", ", p.Value.Values)), StringComparer.OrdinalIgnoreCase);
    }
}
