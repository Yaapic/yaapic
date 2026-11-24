using System.Diagnostics;
using System.Text;
using VYaml;
using VYaml.Parser;

// Throwaway spike (M5-02): VYaml's low-level YamlParser -> generic tree under Native AOT.
foreach (var (name, bytes) in Samples.All())
{
    try
    {
        var sw = Stopwatch.StartNew();
        var tree = TreeReader.Read(bytes);
        sw.Stop();
        var text = Printer.Print(tree);
        Console.WriteLine($"== {name} ({sw.ElapsedMilliseconds} ms)");
        if (text.Length > 600) text = text[..600] + "...";
        Console.WriteLine(text);
    }
    catch (YamlException e) when (e is YamlParserException)
    {
        Console.WriteLine($"== {name}: YamlParserException :: {e.Message}");
    }
    catch (YamlException e)
    {
        Console.WriteLine($"== {name}: {e.GetType().Name} (base {e.GetType().BaseType?.Name}) :: {e.Message}");
    }
    catch (FormatException e)
    {
        Console.WriteLine($"== {name}: FormatException :: {e.Message}");
    }
}

/// <summary>A scalar keeps its raw text and the kind decided from the parser (only plain scalars can be non-string).</summary>
internal enum Kind { String, Number, Bool, Null }

internal sealed record Scalar(string Text, Kind Kind);

internal static class TreeReader
{
    private const int MaxDepth = 64;
    private const int MaxNodes = 1_000_000;

    public static object? Read(byte[] bytes)
    {
        var parser = YamlParser.FromBytes(bytes);
        parser.SkipHeader();
        if (parser.CurrentEventType == ParseEventType.StreamEnd) return null;
        var anchors = new Dictionary<string, object?>();
        var count = 0;
        return Node(ref parser, anchors, 0, ref count);
    }

    private static object? Node(ref YamlParser p, Dictionary<string, object?> anchors, int depth, ref int count)
    {
        if (depth > MaxDepth) throw new FormatException("YAML nested too deeply");
        if (++count > MaxNodes) throw new FormatException("YAML expands to too many nodes");
        string? anchor = p.TryGetCurrentAnchor(out var a) ? a.Name : null;
        if (p.CurrentEventType == ParseEventType.Alias)
        {
            if (anchor is null || !anchors.TryGetValue(anchor, out var target)) throw new FormatException($"Unknown alias '{anchor}'");
            var copy = Clone(target, ref count);
            p.Read();
            return copy;
        }
        object? result;
        switch (p.CurrentEventType)
        {
            case ParseEventType.Scalar:
                result = Probe.Classify(ref p);
                p.Read();
                break;
            case ParseEventType.SequenceStart:
                p.Read();
                var list = new List<object?>();
                while (p.CurrentEventType != ParseEventType.SequenceEnd) list.Add(Node(ref p, anchors, depth + 1, ref count));
                p.Read();
                result = list;
                break;
            case ParseEventType.MappingStart:
                p.Read();
                var map = new Dictionary<string, object?>();
                var merges = new List<Dictionary<string, object?>>();
                while (p.CurrentEventType != ParseEventType.MappingEnd)
                {
                    var key = Node(ref p, anchors, depth + 1, ref count) as Scalar
                        ?? throw new FormatException("Non-scalar mapping key");
                    var value = Node(ref p, anchors, depth + 1, ref count);
                    if (key.Kind == Kind.String && key.Text == "<<")
                    {
                        if (value is Dictionary<string, object?> one) merges.Add(one);
                        else if (value is List<object?> many) merges.AddRange(many.OfType<Dictionary<string, object?>>());
                    }
                    else map[key.Text] = value;
                }
                p.Read();
                foreach (var m in merges) foreach (var kv in m) map.TryAdd(kv.Key, kv.Value); // own keys win
                result = map;
                break;
            default:
                throw new FormatException($"Unexpected event {p.CurrentEventType}");
        }
        if (anchor is not null) anchors[anchor] = result;
        return result;
    }

    private static object? Clone(object? n, ref int count)
    {
        if (++count > MaxNodes) throw new FormatException("YAML expands to too many nodes");
        switch (n)
        {
            case List<object?> l:
                var nl = new List<object?>(l.Count);
                foreach (var i in l) nl.Add(Clone(i, ref count));
                return nl;
            case Dictionary<string, object?> d:
                var nd = new Dictionary<string, object?>(d.Count);
                foreach (var kv in d) nd[kv.Key] = Clone(kv.Value, ref count);
                return nd;
            default: return n;
        }
    }
}

internal static partial class Probe
{
    private static readonly System.Text.RegularExpressions.Regex Number =
        new(@"^-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?$", System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>VYaml exposes no scalar style, but IsNullScalar/TryGetScalarAsBool/Double/Int64 succeed on plain scalars only.</summary>
    public static Scalar Classify(ref YamlParser p)
    {
        var text = p.GetScalarAsString() ?? "";
        if (p.IsNullScalar()) return new Scalar(text, Kind.Null);
        if (p.TryGetScalarAsBool(out _) && text is "true" or "false" or "True" or "False" or "TRUE" or "FALSE") return new Scalar(text, Kind.Bool);
        if ((p.TryGetScalarAsInt64(out _) || p.TryGetScalarAsDouble(out _)) && Number.IsMatch(text)) return new Scalar(text, Kind.Number);
        return new Scalar(text, Kind.String);
    }
}

internal static class Printer
{
    public static string Print(object? n) => n switch
    {
        null => "null",
        Scalar s => s.Kind + ":" + s.Text.Replace("\n", "\\n"),
        List<object?> l => "[" + string.Join(", ", l.Select(Print)) + "]",
        Dictionary<string, object?> d => "{" + string.Join(", ", d.Select(kv => kv.Key + ": " + Print(kv.Value))) + "}",
        _ => n.ToString()!,
    };
}

internal static class Samples
{
    public static List<(string, byte[])> All()
    {
        var r = new List<(string, byte[])>();
        void Add(string n, string s) => r.Add((n, Encoding.UTF8.GetBytes(s)));
        Add("openapi", "openapi: 3.0.3\ninfo:\n  title: Pets\n  version: '1.0'\npaths:\n  /pets:\n    get:\n      responses:\n        '200':\n          content:\n            application/json:\n              schema:\n                $ref: '#/components/schemas/Pet'\ncomponents:\n  schemas:\n    Pet:\n      type: object\n");
        Add("scalars", "a: '1.0'\nb: 1.0\nc: yes\nd: null\ne: ~\nf:\ng: \"true\"\nh: true\ni: 'null'\n");
        Add("numbers", "a: 12\nb: '12'\nc: -0\nd: 1e3\ne: 0.5\nf: .5\ng: +1\nh: 0x1F\ni: 1_000\nj: 007\nk: 1.\nl: .inf\nm: 'true'\nn: True\no: NO\np: Null\nq: 'null'\nr: \"~\"\n");
        Add("blocks", "lit: |\n  line1\n  line2\nfold: >\n  a\n  b\n");
        Add("flow", "m: {a: 1, b: [x, y]}\n");
        Add("anchor-merge", "base: &b\n  x: 1\n  y: 2\nuse:\n  <<: *b\n  y: 3\nlist: &l [1, 2]\ncopy: *l\n");
        Add("doc-markers", "---\na: 1\n...\n");
        Add("multi-doc", "a: 1\n---\nb: 2\n");
        Add("comments", "# top\na: 1 # trailing\n# mid\nb: 2\n");
        Add("crlf", "a: 1\r\nb:\r\n  - x\r\n  - y\r\nc: |\r\n  l1\r\n  l2\r\n");
        r.Add(("bom", [0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes("a: 1\n")]));
        Add("unclosed-flow", "a: [1, 2\nb: 3\n");
        Add("bad-anchor-alias", "a: *nope\n");
        Add("tab-error", "a:\n\t- x\n  b: 2\n");
        var sb = new StringBuilder();
        for (var i = 0; i < 100000; i++) sb.Append("- item").Append(i).Append('\n');
        Add("100k-sequence", sb.ToString());
        var deep = new StringBuilder();
        for (var i = 0; i < 200; i++) deep.Append(' ', i * 2).Append("k:\n");
        deep.Append(' ', 400).Append("v: 1\n");
        Add("depth-200", deep.ToString());
        var bomb = new StringBuilder("a: &a [x,x,x,x,x,x,x,x,x,x]\n");
        var prev = "a";
        for (var i = 1; i <= 8; i++)
        {
            var cur = ((char)('a' + i)).ToString();
            bomb.Append(cur + ": &" + cur + " [" + string.Join(",", Enumerable.Repeat("*" + prev, 10)) + "]\n");
            prev = cur;
        }
        Add("billion-laughs", bomb.ToString());
        return r;
    }
}
