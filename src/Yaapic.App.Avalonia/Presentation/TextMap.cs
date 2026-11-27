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

    /// <summary>Gets the "no collections" line of the export window from the format's need for a collection and the collection count.</summary>
    public static FuncMultiValueConverter<object?, string> ExportEmpty { get; } =
        new(v => v.ToArray() is [bool requires, int count] && requires && count == 0 ? Strings.ExportNoCollections : "");

    /// <summary>Gets the "saved" line shown after an export, empty when nothing was saved.</summary>
    public static FuncValueConverter<string?, string> ExportSaved { get; } = new(n => n is null ? "" : Strings.ExportSaved(n));

    /// <summary>Gets the "n of m selected" line of the Runner setup from the selected and total counts.</summary>
    public static FuncMultiValueConverter<object?, string> RunnerSelected { get; } =
        new(v => v.ToArray() is [int selected, int total] ? Strings.RunnerSelectedCount(selected, total) : "");

    /// <summary>Gets the data file summary line of the Runner setup from the file name, row count and column count; empty without a file.</summary>
    public static FuncMultiValueConverter<object?, string> RunnerData { get; } =
        new(v => v.ToArray() is [string file, int rows, int columns] ? Strings.RunnerDataSummary(file, rows, columns) : "");
}
