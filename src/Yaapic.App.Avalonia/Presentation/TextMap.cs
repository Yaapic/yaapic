using Avalonia.Data.Converters;
using Yaapic.Localization;

namespace Yaapic.App.Avalonia.Presentation;

/// <summary>Converters that turn view model values into localized display text.</summary>
public static class TextMap
{
    /// <summary>Gets the "Detected: {format}" line of the import window, empty when nothing is detected.</summary>
    public static FuncValueConverter<string?, string> ImportDetected { get; } = new(f => f is null ? "" : Strings.ImportDetected(f));

    /// <summary>Gets the "{0} folders, {1} requests, ..." counts line from folders, requests, environments and global variables.</summary>
    public static FuncMultiValueConverter<object?, string> ImportCounts { get; } =
        new(v => v.ToArray() is [var folders, var requests, var environments, var globals] ? Strings.ImportCounts(folders, requests, environments, globals) : "");
}
