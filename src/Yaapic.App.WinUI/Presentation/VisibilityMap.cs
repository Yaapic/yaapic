using Microsoft.UI.Xaml;
using Yaapic.Domain.Http;

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

    /// <summary>Gets <see cref="Visibility.Visible"/> when <paramref name="value"/> is <see langword="true"/>.</summary>
    /// <param name="value">The flag to test.</param>
    /// <returns>The visibility.</returns>
    public static Visibility WhenTrue(bool value) => value ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Gets <see cref="Visibility.Visible"/> when <paramref name="value"/> is <see langword="false"/>.</summary>
    /// <param name="value">The flag to test.</param>
    /// <returns>The visibility.</returns>
    public static Visibility WhenFalse(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Gets <see cref="Visibility.Visible"/> when the method is GET, which is sent without a body.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <returns>The visibility.</returns>
    public static Visibility WhenGet(HttpMethodKind method) => method == HttpMethodKind.Get ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Gets <see cref="Visibility.Visible"/> when the body is raw text.</summary>
    /// <param name="kind">The body kind.</param>
    /// <returns>The visibility.</returns>
    public static Visibility WhenRaw(BodyKind kind) => kind == BodyKind.Raw ? Visibility.Visible : Visibility.Collapsed;
}
