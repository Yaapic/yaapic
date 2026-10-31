namespace Yaapic.App.WinUI.Presentation;

/// <summary>Text helpers used through <c>x:Bind</c> function binding.</summary>
public static class TextMap
{
    /// <summary>Gets <paramref name="text"/> in upper case, as shown in table headers.</summary>
    /// <param name="text">The text to convert.</param>
    /// <returns>The upper-case text.</returns>
    public static string Upper(string text) => text.ToUpperInvariant();

    /// <summary>Gets <paramref name="prefix"/> followed by <paramref name="value"/>, as used for accessible names.</summary>
    /// <param name="prefix">The leading text.</param>
    /// <param name="value">The value appended to it.</param>
    /// <returns>The joined text.</returns>
    public static string Prefixed(string prefix, string value) => prefix + value;
}
