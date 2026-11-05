using Avalonia.Data.Converters;

namespace Yaapic.App.Avalonia.Presentation;

/// <summary>Formats palette shortcut hints for the host platform, without reflection.</summary>
public static class ShortcutText
{
    /// <summary>Gets a converter that adapts a shortcut hint to the host platform.</summary>
    public static FuncValueConverter<string?, string?> ForHost { get; } = new(s => ForPlatform(s, OperatingSystem.IsMacOS()));

    /// <summary>Shows <c>Ctrl</c> as <c>⌘</c> and <c>Alt</c> as <c>⌥</c> on macOS; other platforms keep the text.</summary>
    /// <param name="shortcut">The shortcut hint, for example <c>Ctrl+Shift+E</c>.</param>
    /// <param name="macOS">Whether the host is macOS.</param>
    /// <returns>The text to display.</returns>
    public static string? ForPlatform(string? shortcut, bool macOS) =>
        macOS && shortcut is not null ? shortcut.Replace("Ctrl+", "⌘", StringComparison.Ordinal).Replace("Alt+", "⌥", StringComparison.Ordinal) : shortcut;
}
