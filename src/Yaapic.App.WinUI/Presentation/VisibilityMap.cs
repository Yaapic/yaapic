using Microsoft.UI.Xaml;

namespace Yaapic.App.WinUI.Presentation;

/// <summary>Visibility helpers used through <c>x:Bind</c> function binding.</summary>
public static class VisibilityMap
{
    /// <summary>Gets <see cref="Visibility.Visible"/> when <paramref name="value"/> is <see langword="null"/>.</summary>
    /// <param name="value">The value to test.</param>
    /// <returns>The visibility.</returns>
    public static Visibility WhenNull(object? value) => value is null ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Gets whether <paramref name="text"/> holds a message.</summary>
    /// <param name="text">The text to test.</param>
    /// <returns><see langword="true"/> when the text is not empty.</returns>
    public static bool HasText(string? text) => !string.IsNullOrEmpty(text);
}
