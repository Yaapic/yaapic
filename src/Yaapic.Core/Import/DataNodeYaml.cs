using System.Globalization;
using System.Text;

namespace Yaapic.Core.Import;

/// <summary>Writes <see cref="DataNode"/> trees as YAML (block style, double-quoted strings, no library).</summary>
public static class DataNodeYaml
{
    /// <summary>Writes a tree as YAML with <c>\n</c> line ends, two-space indentation and a single trailing newline.</summary>
    /// <param name="node">The tree.</param>
    /// <returns>The YAML text.</returns>
    public static string Write(DataNode node)
    {
        var sb = new StringBuilder();
        if (IsBlock(node))
        {
            WriteBlock(sb, node, 0, skipFirstIndent: false);
        }
        else
        {
            sb.Append(Inline(node)).Append('\n');
        }

        return sb.ToString();
    }

    private static bool IsBlock(DataNode node) =>
        (node.IsObject && node.Members.Count > 0) || (node.IsArray && node.Items.Count > 0);

    private static string Inline(DataNode node) => node.Kind switch
    {
        DataKind.Object => "{}",
        DataKind.Array => "[]",
        DataKind.Number => node.AsString()!,
        DataKind.String => Quote(node.AsString()!),
        DataKind.Bool => node.AsBool()!.Value ? "true" : "false",
        _ => "null",
    };

    // Recursion depth follows the tree depth, which the parsers bound.
    private static void WriteBlock(StringBuilder sb, DataNode node, int indent, bool skipFirstIndent)
    {
        if (node.IsObject)
        {
            for (var i = 0; i < node.Members.Count; i++)
            {
                var (key, value) = node.Members[i];
                if (i > 0 || !skipFirstIndent)
                {
                    sb.Append(' ', indent);
                }

                sb.Append(Key(key)).Append(':');
                if (IsBlock(value))
                {
                    sb.Append('\n');
                    WriteBlock(sb, value, indent + 2, skipFirstIndent: false);
                }
                else
                {
                    sb.Append(' ').Append(Inline(value)).Append('\n');
                }
            }

            return;
        }

        foreach (var item in node.Items)
        {
            sb.Append(' ', indent);
            if (item.IsObject && IsBlock(item))
            {
                sb.Append("- ");
                WriteBlock(sb, item, indent + 2, skipFirstIndent: true);
            }
            else if (IsBlock(item))
            {
                sb.Append("-\n");
                WriteBlock(sb, item, indent + 2, skipFirstIndent: false);
            }
            else
            {
                sb.Append("- ").Append(Inline(item)).Append('\n');
            }
        }
    }

    private static string Key(string key) => IsBareKey(key) ? key : Quote(key);

    private static bool IsBareKey(string key)
    {
        if (key.Length == 0 || !(char.IsAsciiLetter(key[0]) || key[0] == '_'))
        {
            return false;
        }

        foreach (var c in key)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c == '_' || c == '-'))
            {
                return false;
            }
        }

        return true;
    }

    private static string Quote(string text)
    {
        var sb = new StringBuilder(text.Length + 2).Append('"');
        foreach (var c in text)
        {
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                case < ' ':
                    sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    break;
                default: sb.Append(c); break;
            }
        }

        return sb.Append('"').ToString();
    }
}
