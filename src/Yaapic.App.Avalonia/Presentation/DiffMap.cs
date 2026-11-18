using Avalonia.Data.Converters;
using Avalonia.Media;
using Yaapic.Core.Diff;

namespace Yaapic.App.Avalonia.Presentation;

/// <summary>Diff row helpers used as converters by the compiled bindings of the Diff tab.</summary>
public static class DiffMap
{
    /// <summary>Converts a line operation to the row background: success surface for added, danger surface for removed.</summary>
    public static FuncValueConverter<DiffOp, IBrush?> Background { get; } = new(op => op switch
    {
        DiffOp.Added => BrushMap.Get("YaapicSuccessBackgroundBrush"),
        DiffOp.Removed => BrushMap.Get("YaapicDangerBackgroundBrush"),
        _ => Brushes.Transparent,
    });

    /// <summary>Converts a line operation to its one-character marker (<c>+</c>, <c>-</c> or empty).</summary>
    public static FuncValueConverter<DiffOp, string> Marker { get; } = new(op => op switch
    {
        DiffOp.Added => "+",
        DiffOp.Removed => "-",
        _ => "",
    });

    /// <summary>Converts <c>UseBaseline</c> to the index of the Compare-with option.</summary>
    public static FuncValueConverter<bool, int> CompareIndex { get; } = new(b => b ? 1 : 0);

    /// <summary>Converts <c>(HasComparison, UnavailableReason)</c> to <see langword="true"/> when the comparison can be shown.</summary>
    public static FuncMultiValueConverter<object?, bool> Comparable { get; } = new(v => Parts(v) is (true, false));

    /// <summary>Converts <c>(HasComparison, UnavailableReason)</c> to <see langword="true"/> when two responses are still needed.</summary>
    public static FuncMultiValueConverter<object?, bool> NeedTwo { get; } = new(v => Parts(v) is (false, false));

    private static (bool HasComparison, bool HasReason) Parts(IEnumerable<object?> values) =>
        values.ToList() is [bool has, var reason] ? (has, reason is string { Length: > 0 }) : (false, false);
}
