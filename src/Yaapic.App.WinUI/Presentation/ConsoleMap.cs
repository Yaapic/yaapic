using Microsoft.UI.Text;
using Yaapic.ViewModels.Console;

namespace Yaapic.App.WinUI.Presentation;

/// <summary>Console panel helpers used through <c>x:Bind</c> function binding.</summary>
public static class ConsoleMap
{
    /// <summary>Gets the font weight of a console line: group headers are bold.</summary>
    /// <param name="kind">The kind of line.</param>
    /// <returns>Semi-bold for headers, else normal.</returns>
    public static Windows.UI.Text.FontWeight Weight(ConsoleLineKind kind) =>
        kind == ConsoleLineKind.Header ? FontWeights.SemiBold : FontWeights.Normal;
}
