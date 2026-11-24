using System.Text;

namespace Yaapic.Core.Scripting;

/// <summary>
/// Renames the <c>yaapic</c> global to <c>pm</c> in a JavaScript script: the inverse of the Postman import conversion.
/// Comments, strings and template literals are copied unchanged.
/// </summary>
public static class YaapicToPostmanRewriter
{
    private const string Root = "yaapic.";

    /// <summary>Rewrites every <c>yaapic.</c> member access to <c>pm.</c>.</summary>
    /// <param name="source">The JavaScript source.</param>
    /// <returns>The rewritten source.</returns>
    // shortcut: regular-expression literals and `${...}` inside template literals are not recognised; upgrade with a real tokenizer if reports show it.
    public static string Rewrite(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        StringBuilder sb = new(source.Length);
        var i = 0;
        while (i < source.Length)
        {
            var c = source[i];
            int end;
            if (c == '/' && Next(source, i) == '/')
            {
                end = source.IndexOf('\n', i);
                end = end < 0 ? source.Length : end;
            }
            else if (c == '/' && Next(source, i) == '*')
            {
                end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                end = end < 0 ? source.Length : end + 2;
            }
            else if (c is '\'' or '"' or '`')
            {
                end = StringEnd(source, i);
            }
            else if (c == 'y' && string.CompareOrdinal(source, i, Root, 0, Root.Length) == 0 && !IsIdentPartOrDot(i == 0 ? ' ' : source[i - 1]))
            {
                sb.Append("pm.");
                i += Root.Length;
                continue;
            }
            else
            {
                end = i + 1;
            }

            sb.Append(source, i, end - i);
            i = end;
        }

        return sb.ToString();
    }

    private static char Next(string s, int i) => i + 1 < s.Length ? s[i + 1] : '\0';

    private static bool IsIdentPartOrDot(char c) => char.IsLetterOrDigit(c) || c is '_' or '$' or '.';

    private static int StringEnd(string s, int start)
    {
        var quote = s[start];
        for (var i = start + 1; i < s.Length; i++)
        {
            if (s[i] == '\\')
            {
                i++;
            }
            else if (s[i] == quote || (quote != '`' && s[i] == '\n'))
            {
                return i + 1;
            }
        }

        return s.Length;
    }
}
