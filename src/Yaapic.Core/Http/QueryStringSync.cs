using System.Text;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Http;

/// <summary>A URL split into the text before the query, its query entries and its fragment.</summary>
/// <param name="Prefix">Everything before the first <c>?</c> or <c>#</c>.</param>
/// <param name="Params">The decoded query entries, all enabled.</param>
/// <param name="Fragment">The fragment including its <c>#</c>, or an empty string.</param>
public sealed record UrlParts(string Prefix, IReadOnlyList<KeyValueEntry> Params, string Fragment);

/// <summary>Splits a URL into prefix, query entries and fragment, and puts them back together.</summary>
public static class QueryStringSync
{
    /// <summary>Splits a URL; the prefix is not parsed, so it may hold variables or be incomplete.</summary>
    /// <param name="url">The URL text as typed.</param>
    /// <returns>The prefix, the decoded query entries and the fragment.</returns>
    public static UrlParts Parse(string url)
    {
        ArgumentNullException.ThrowIfNull(url);

        var hash = url.IndexOf('#');
        var fragment = hash < 0 ? "" : url[hash..];
        var head = hash < 0 ? url : url[..hash];
        var question = head.IndexOf('?');
        if (question < 0)
        {
            return new UrlParts(head, [], fragment);
        }

        List<KeyValueEntry> entries = [];
        foreach (var segment in head[(question + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = segment.IndexOf('=');
            entries.Add(eq < 0
                ? new KeyValueEntry(Uri.UnescapeDataString(segment), "")
                : new KeyValueEntry(Uri.UnescapeDataString(segment[..eq]), Uri.UnescapeDataString(segment[(eq + 1)..])));
        }

        return new UrlParts(head[..question], entries, fragment);
    }

    /// <summary>Builds a URL from parsed parts.</summary>
    /// <param name="parts">The parts to join.</param>
    /// <returns>The URL text.</returns>
    public static string Compose(UrlParts parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        return Compose(parts.Prefix, parts.Params, parts.Fragment);
    }

    /// <summary>Builds a URL, escaping keys and values but keeping <c>{{name}}</c> tokens readable.</summary>
    /// <param name="prefix">The text before the query.</param>
    /// <param name="enabledParams">The entries to write; entries with an empty key are skipped.</param>
    /// <param name="fragment">The fragment including its <c>#</c>, or an empty string.</param>
    /// <returns>The URL text.</returns>
    public static string Compose(string prefix, IReadOnlyList<KeyValueEntry> enabledParams, string fragment)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(enabledParams);
        ArgumentNullException.ThrowIfNull(fragment);

        StringBuilder sb = new(prefix);
        var separator = '?';
        foreach (var entry in enabledParams)
        {
            if (entry.Key.Length == 0)
            {
                continue;
            }

            sb.Append(separator).Append(Escape(entry.Key)).Append('=').Append(Escape(entry.Value));
            separator = '&';
        }

        return sb.Append(fragment).ToString();
    }

    private static string Escape(string text)
    {
        StringBuilder sb = new();
        var start = 0;
        while (start < text.Length)
        {
            var open = text.IndexOf("{{", start, StringComparison.Ordinal);
            var close = open < 0 ? -1 : FindTokenEnd(text, open + 2);
            if (close < 0)
            {
                // No complete token at this "{{": escape through its first brace and look again from the second.
                var stop = open < 0 ? text.Length : open + 1;
                sb.Append(Uri.EscapeDataString(text[start..stop]));
                start = stop;
                continue;
            }

            sb.Append(Uri.EscapeDataString(text[start..open])).Append(text, open, close + 2 - open);
            start = close + 2;
        }

        return sb.ToString();
    }

    // Index of the closing "}}" when only non-brace characters follow `from`, else -1.
    private static int FindTokenEnd(string text, int from)
    {
        for (var i = from; i < text.Length; i++)
        {
            if (text[i] == '}')
            {
                return i + 1 < text.Length && text[i + 1] == '}' ? i : -1;
            }

            if (text[i] == '{')
            {
                return -1;
            }
        }

        return -1;
    }
}
