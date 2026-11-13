namespace Yaapic.ViewModels.Requests;

/// <summary>Finds the <c>{{name</c> being typed at the caret and applies an accepted suggestion to the text.</summary>
public static class VariableCompletion
{
    /// <summary>Finds the unclosed <c>{{</c> before the caret.</summary>
    /// <param name="text">The text of the box.</param>
    /// <param name="caret">The caret index.</param>
    /// <param name="start">The index of the <c>{{</c>.</param>
    /// <param name="prefix">What is typed after it, up to the caret.</param>
    /// <returns><see langword="true"/> when the caret is inside a placeholder that is still being typed.</returns>
    public static bool TryGetPrefix(string? text, int caret, out int start, out string prefix)
    {
        start = -1;
        prefix = "";
        if (string.IsNullOrEmpty(text) || caret < 2 || caret > text.Length)
        {
            return false;
        }

        for (var i = caret - 1; i >= 1; i--)
        {
            var c = text[i];
            if (c == '}')
            {
                return false;
            }

            if (c == '{')
            {
                if (text[i - 1] != '{')
                {
                    return false;
                }

                start = i - 1;
                prefix = text[(i + 1)..caret].TrimStart();
                return true;
            }
        }

        return false;
    }

    /// <summary>Replaces the typed prefix with a variable name and closes the placeholder.</summary>
    /// <param name="text">The text of the box.</param>
    /// <param name="caret">The caret index.</param>
    /// <param name="start">The index of the <c>{{</c>, from <see cref="TryGetPrefix"/>.</param>
    /// <param name="name">The accepted variable name.</param>
    /// <returns>The new text and the caret index, placed after the closing braces.</returns>
    public static (string Text, int Caret) Apply(string text, int caret, int start, string name)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(name);
        var after = text[caret..];
        var trimmed = after.TrimStart(' ');
        var rest = trimmed.StartsWith("}}", StringComparison.Ordinal) ? trimmed[2..] : after;
        var head = text[..(start + 2)] + name + "}}";
        return (head + rest, head.Length);
    }
}
