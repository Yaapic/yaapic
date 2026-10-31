namespace Yaapic.App.WinUI.Presentation;

/// <summary>Opacity helpers used through <c>x:Bind</c> function binding.</summary>
public static class OpacityMap
{
    /// <summary>Gets the opacity of a key/value row: dimmed for the empty placeholder and for disabled rows.</summary>
    /// <param name="isEmpty">Whether the row is the empty placeholder.</param>
    /// <param name="enabled">Whether the row is enabled.</param>
    /// <returns>The opacity.</returns>
    public static double KeyValueRow(bool isEmpty, bool enabled) => isEmpty ? 0.6 : enabled ? 1 : 0.55;
}
