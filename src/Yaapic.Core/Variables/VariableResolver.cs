using System.Text;

namespace Yaapic.Core.Variables;

/// <summary>Replaces <c>{{name}}</c> placeholders from a <see cref="IVariableSource"/>.</summary>
public static class VariableResolver
{
    /// <summary>Resolves the placeholders of a text in a single pass; substituted values are not scanned again.</summary>
    /// <param name="input">The text; null or empty yields an empty result.</param>
    /// <param name="source">The variable source.</param>
    /// <returns>The resolved text and the names that could not be resolved.</returns>
    public static ResolveResult Resolve(string? input, IVariableSource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (string.IsNullOrEmpty(input))
        {
            return new("", []);
        }

        var sb = new StringBuilder(input.Length);
        List<string> unresolved = [];
        var i = 0;
        while (i < input.Length)
        {
            if (input[i] == '{' && i + 1 < input.Length && input[i + 1] == '{')
            {
                var close = input.IndexOf("}}", i + 2, StringComparison.Ordinal);
                if (close >= 0)
                {
                    var raw = input.AsSpan(i + 2, close - i - 2);
                    var name = raw.Trim().ToString();
                    if (name.Length > 0 && raw.IndexOfAny('{', '}') < 0)
                    {
                        if (source.TryGet(name, out var value))
                        {
                            sb.Append(value);
                        }
                        else
                        {
                            sb.Append(input, i, close + 2 - i);
                            if (!unresolved.Contains(name))
                            {
                                unresolved.Add(name);
                            }
                        }

                        i = close + 2;
                        continue;
                    }
                }
            }

            sb.Append(input[i++]);
        }

        return new(sb.ToString(), unresolved);
    }
}
