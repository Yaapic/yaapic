namespace Yaapic.Core.Import.Bruno;

/// <summary>A tolerant parser for the Bru markup; it never throws on malformed content.</summary>
internal static class BruParser
{
    private const string Indent = "  ";
    private const string MultiLineMarker = "'''";

    private static readonly HashSet<string> TextBlocks = new(StringComparer.Ordinal)
    {
        "body:json", "body:text", "body:xml", "body:sparql", "body:graphql", "body:graphql:vars",
        "script:pre-request", "script:post-response", "tests", "docs",
    };

    /// <summary>Parses a Bru file.</summary>
    /// <param name="text">The file content.</param>
    /// <returns>The blocks found; text outside blocks is ignored.</returns>
    public static BruFile Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var blocks = new List<BruBlock>();
        var i = 0;
        while (i < lines.Length)
        {
            if (!TryReadHeader(lines[i], out var name, out var open))
            {
                i++;
                continue;
            }

            var close = open == '{' ? "}" : "]";
            var start = ++i;
            while (i < lines.Length && lines[i] != close)
            {
                i++;
            }

            var inner = lines[start..i];
            i++; // skip the closing line (or run past the end of an unterminated block)
            blocks.Add(Build(name, open, inner));
        }

        return new BruFile(blocks);
    }

    private static bool TryReadHeader(string line, out string name, out char open)
    {
        name = string.Empty;
        open = '\0';
        var end = 0;
        while (end < line.Length && (char.IsAsciiLetterOrDigit(line[end]) || line[end] is ':' or '_' or '-'))
        {
            end++;
        }

        if (end == 0)
        {
            return false;
        }

        var rest = line[end..].Trim(' ', '\t');
        if (rest is not ("{" or "["))
        {
            return false;
        }

        name = line[..end];
        open = rest[0];
        return true;
    }

    private static BruBlock Build(string name, char open, string[] inner)
    {
        if (open == '[')
        {
            var items = inner
                .SelectMany(l => l.Split(','))
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();
            return new BruBlock(name, BruBlockKind.List, [], string.Empty, items);
        }

        if (TextBlocks.Contains(name))
        {
            return new BruBlock(name, BruBlockKind.Text, [], string.Join('\n', inner.Select(Unindent)), []);
        }

        return new BruBlock(name, BruBlockKind.Dictionary, ReadPairs(inner), string.Empty, []);
    }

    private static string Unindent(string line) =>
        line.StartsWith(Indent, StringComparison.Ordinal) ? line[Indent.Length..] : line;

    private static List<BruPair> ReadPairs(string[] inner)
    {
        var pairs = new List<BruPair>();
        for (var i = 0; i < inner.Length; i++)
        {
            var line = inner[i].Trim();
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon < 0)
            {
                continue;
            }

            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim();
            var enabled = !key.StartsWith('~');
            if (!enabled)
            {
                key = key[1..].Trim();
            }

            if (value == MultiLineMarker)
            {
                var body = new List<string>();
                i++;
                while (i < inner.Length && inner[i].Trim() != MultiLineMarker)
                {
                    body.Add(Unindent(inner[i++]));
                }

                value = string.Join('\n', body);
            }

            pairs.Add(new BruPair(key, value, enabled));
        }

        return pairs;
    }
}
