using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Yaapic.Domain.Http;

namespace Yaapic.App.Avalonia.Presentation;

/// <summary>Maps domain values to the generated <c>Yaapic*</c> brushes; exposed as converters for compiled bindings.</summary>
public static class BrushMap
{
    /// <summary>Converts an HTTP method to the brush that colors its label.</summary>
    public static FuncValueConverter<HttpMethodKind, IBrush?> MethodBrush { get; } = new(m => Method(m));

    /// <summary>Gets the brush that colors an HTTP method label.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <returns>The method brush from the application resources, or <see langword="null"/> when missing.</returns>
    public static IBrush? Method(HttpMethodKind method) =>
        Get(method == HttpMethodKind.Post ? "YaapicMethodPostBrush" : "YaapicMethodGetBrush");

    internal static IBrush? Get(string key) =>
        Application.Current is { } app && app.TryFindResource(key, app.ActualThemeVariant, out var brush) ? brush as IBrush : null;
}

