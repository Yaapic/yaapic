namespace Yaapic.Domain.Scripting;

/// <summary>Text conversion for <see cref="ScriptLanguage"/>.</summary>
public static class ScriptLanguageExtensions
{
    /// <summary>Gets the lower-case file-format name of the language.</summary>
    /// <param name="language">The language.</param>
    /// <returns><c>javascript</c> or <c>lua</c>.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="language"/> is not a defined value.</exception>
    public static string ToText(this ScriptLanguage language) => language switch
    {
        ScriptLanguage.JavaScript => "javascript",
        ScriptLanguage.Lua => "lua",
        _ => throw new ArgumentOutOfRangeException(nameof(language), language, null),
    };

    /// <summary>Parses a language name, ignoring case; the text is not trimmed.</summary>
    /// <param name="text">The text to parse.</param>
    /// <param name="language">The parsed language when the result is <see langword="true"/>.</param>
    /// <returns><see langword="true"/> when <paramref name="text"/> names a supported language.</returns>
    public static bool TryParse(string? text, out ScriptLanguage language)
    {
        if (string.Equals(text, "javascript", StringComparison.OrdinalIgnoreCase))
        {
            language = ScriptLanguage.JavaScript;
            return true;
        }

        if (string.Equals(text, "lua", StringComparison.OrdinalIgnoreCase))
        {
            language = ScriptLanguage.Lua;
            return true;
        }

        language = default;
        return false;
    }
}
