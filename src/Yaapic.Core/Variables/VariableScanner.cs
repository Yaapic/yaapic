namespace Yaapic.Core.Variables;

/// <summary>Lists the <c>{{name}}</c> placeholders of a text, using the token rules of <see cref="VariableResolver"/>.</summary>
public static class VariableScanner
{
    /// <summary>Scans a text for placeholders.</summary>
    /// <param name="text">The text; null or empty yields no names.</param>
    /// <returns>The distinct trimmed names, in order of first occurrence.</returns>
    public static IReadOnlyList<string> Scan(string? text)
    {
        List<string> names = [];
        foreach (var span in ScanSpans(text))
        {
            if (!names.Contains(span.Name))
            {
                names.Add(span.Name);
            }
        }

        return names;
    }

    /// <summary>Scans a text for placeholders and reports where each one sits.</summary>
    /// <param name="text">The text; null or empty yields no spans.</param>
    /// <returns>Every placeholder, in order, never overlapping; anything that is not a well-formed token is plain text.</returns>
    public static IReadOnlyList<VariableSpan> ScanSpans(string? text)
    {
        List<VariableSpan> spans = [];
        if (string.IsNullOrEmpty(text))
        {
            return spans;
        }

        var i = 0;
        while (i < text.Length)
        {
            if (text[i] == '{' && i + 1 < text.Length && text[i + 1] == '{')
            {
                var close = text.IndexOf("}}", i + 2, StringComparison.Ordinal);
                if (close >= 0)
                {
                    var raw = text.AsSpan(i + 2, close - i - 2);
                    var name = raw.Trim().ToString();
                    if (name.Length > 0 && raw.IndexOfAny('{', '}') < 0)
                    {
                        spans.Add(new VariableSpan(i, close + 2 - i, name));
                        i = close + 2;
                        continue;
                    }
                }
            }

            i++;
        }

        return spans;
    }

    /// <summary>Tells whether a name could be written as a placeholder: non-empty once trimmed and free of braces.</summary>
    /// <param name="name">The candidate name.</param>
    /// <returns><see langword="true"/> when <c>{{name}}</c> would be scanned as that name.</returns>
    public static bool IsValidName(string? name) =>
        name is not null && name.AsSpan().Trim().Length > 0 && name.AsSpan().IndexOfAny('{', '}') < 0;
}
