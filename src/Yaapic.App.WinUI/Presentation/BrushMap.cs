using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Yaapic.Domain.Http;

namespace Yaapic.App.WinUI.Presentation;

/// <summary>Maps domain values to the generated <c>Yaapic*</c> brushes; used through <c>x:Bind</c> function binding.</summary>
public static class BrushMap
{
    /// <summary>Gets the brush that colors an HTTP method label.</summary>
    /// <param name="method">The HTTP method.</param>
    /// <returns>The method brush from the application resources.</returns>
    public static Brush Method(HttpMethodKind method) =>
        (Brush)Application.Current.Resources[method == HttpMethodKind.Post ? "YaapicMethodPostBrush" : "YaapicMethodGetBrush"];
}
