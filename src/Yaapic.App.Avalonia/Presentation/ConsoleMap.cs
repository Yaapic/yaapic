using Avalonia.Data.Converters;
using Avalonia.Media;
using Yaapic.Domain.Http;
using Yaapic.ViewModels.Console;

namespace Yaapic.App.Avalonia.Presentation;

/// <summary>Console panel and status bar converters. Colours come from style classes, so they follow the theme without a rebuild.</summary>
public static class ConsoleMap
{
    /// <summary>Converts a line kind to whether it is a warning.</summary>
    public static FuncValueConverter<ConsoleLineKind, bool> IsWarn { get; } = new(k => k == ConsoleLineKind.Warn);

    /// <summary>Converts a line kind to whether it is an error or a failed test.</summary>
    public static FuncValueConverter<ConsoleLineKind, bool> IsErrorKind { get; } = new(k => k is ConsoleLineKind.Error or ConsoleLineKind.TestFailed);

    /// <summary>Converts a line kind to whether it is a passed test.</summary>
    public static FuncValueConverter<ConsoleLineKind, bool> IsPassed { get; } = new(k => k == ConsoleLineKind.TestPassed);

    /// <summary>Converts a line kind to whether it is an information line.</summary>
    public static FuncValueConverter<ConsoleLineKind, bool> IsInfo { get; } = new(k => k == ConsoleLineKind.Info);

    /// <summary>Converts a line kind to whether it is a group header.</summary>
    public static FuncValueConverter<ConsoleLineKind, bool> IsHeader { get; } = new(k => k == ConsoleLineKind.Header);

    /// <summary>Converts a count to whether it is positive.</summary>
    public static FuncValueConverter<int, bool> IsPositive { get; } = new(n => n > 0);

    /// <summary>Converts a list of lines to its first line, or an empty string.</summary>
    public static FuncValueConverter<IReadOnlyList<string>?, string> FirstLine { get; } = new(lines => lines is { Count: > 0 } ? lines[0] : "");

    /// <summary>Converts a list of lines to the lines joined with line breaks, as shown in a tooltip.</summary>
    public static FuncValueConverter<IReadOnlyList<string>?, string> JoinLines { get; } = new(lines => lines is null ? "" : string.Join('\n', lines));

    /// <summary>Converts an HTTP verb (<c>GET</c>, ...) to the brush that colors it; unknown verbs read as GET.</summary>
    public static FuncValueConverter<string?, IBrush?> VerbBrush { get; } =
        new(verb => BrushMap.Method(verb is not null && HttpMethodKindExtensions.TryParse(verb, out var kind) ? kind : HttpMethodKind.Get));

    /// <summary>Converts the empty-text flag of a text value to whether it has text.</summary>
    public static FuncValueConverter<string?, bool> HasText { get; } = new(s => !string.IsNullOrEmpty(s));
}
