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
        if (string.IsNullOrEmpty(text))
        {
            return names;
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
                        if (!names.Contains(name))
                        {
                            names.Add(name);
                        }

                        i = close + 2;
                        continue;
                    }
                }
            }

            i++;
        }

        return names;
    }
}
