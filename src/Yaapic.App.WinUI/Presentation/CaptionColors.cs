using Microsoft.UI.Windowing;

namespace Yaapic.App.WinUI.Presentation;

/// <summary>The caption-button colours shared by the main window and the tool windows.</summary>
internal static class CaptionColors
{
    // The caption buttons follow the OS theme, not the element theme forced by --theme or the Settings menu: colour them
    // from the tokens (text.primary / text.secondary, overlay.hover) so they stay visible on both surfaces.
    internal static void Apply(AppWindowTitleBar titleBar, bool light)
    {
        var foreground = light ? Windows.UI.Color.FromArgb(0xFF, 0x4A, 0x51, 0x5B) : Windows.UI.Color.FromArgb(0xFF, 0xA3, 0xAA, 0xB4);
        var hoverForeground = light ? Windows.UI.Color.FromArgb(0xFF, 0x16, 0x19, 0x1D) : Windows.UI.Color.FromArgb(0xFF, 0xEC, 0xEE, 0xF1);
        var hover = light ? Windows.UI.Color.FromArgb(0x0D, 0x14, 0x18, 0x1E) : Windows.UI.Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonInactiveForegroundColor = foreground;
        titleBar.ButtonHoverForegroundColor = hoverForeground;
        titleBar.ButtonPressedForegroundColor = hoverForeground;
        titleBar.ButtonHoverBackgroundColor = hover;
    }
}
