using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Yaapic.App.WinUI.Views;

/// <summary>Keeps the request list of a setup pane visible: the options scroll in whatever height is left once the list has its minimum.</summary>
internal static class PaneLayout
{
    private const double MinListHeight = 200;
    private const double MinOptionsHeight = 90;
    private const double PaneChrome = 24 + 16; // padding top and bottom, two row gaps

    /// <summary>Caps the options height whenever the pane or its footer changes size.</summary>
    /// <param name="pane">The pane grid: list row, options row, footer row.</param>
    /// <param name="options">The scrolling options.</param>
    /// <param name="footer">The validation line and the buttons.</param>
    public static void Attach(FrameworkElement pane, ScrollViewer options, FrameworkElement footer)
    {
        void Update() => options.MaxHeight = Math.Max(MinOptionsHeight, pane.ActualHeight - PaneChrome - footer.ActualHeight - MinListHeight);
        pane.SizeChanged += (_, _) => Update();
        footer.SizeChanged += (_, _) => Update();
    }
}
