using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Yaapic.Localization;

namespace Yaapic.App.Avalonia.Presentation;

/// <summary>Waterfall helpers: the bar is placed with star columns, so it stays proportional when the track is resized.</summary>
public static class TimingBarMap
{
    /// <summary>Converts <c>(IsMeasured, OffsetFraction)</c> to the star width of the empty lead before the bar.</summary>
    public static FuncMultiValueConverter<object?, GridLength> Lead { get; } = new(v => Star(v, (m, a, _) => m ? Clamp(a) : 0));

    /// <summary>Converts <c>(IsMeasured, Fraction)</c> to the star width of the bar.</summary>
    public static FuncMultiValueConverter<object?, GridLength> Bar { get; } = new(v => Star(v, (m, a, _) => m ? Clamp(a) : 0));

    /// <summary>Converts <c>(IsMeasured, OffsetFraction, Fraction)</c> to the star width of what remains after the bar.</summary>
    public static FuncMultiValueConverter<object?, GridLength> Rest { get; } = new(v =>
        Star(v, (m, offset, fraction) => m ? Math.Max(0, 1 - Clamp(offset) - Clamp(fraction)) : 1));

    /// <summary>Converts a phase label to its bar brush: the accent for Waiting, a neutral token otherwise.</summary>
    public static FuncValueConverter<string?, IBrush?> BarBrush { get; } = new(label =>
        BrushMap.Get(label == Strings.TimingWaiting ? "YaapicAccentFillBrush" : "YaapicBorderOverlayBrush"));

    private static double Clamp(double v) => Math.Clamp(v, 0, 1);

    private static GridLength Star(IEnumerable<object?> values, Func<bool, double, double, double> width)
    {
        var list = values.ToList();
        var measured = list is [bool m, ..] && m;
        var a = list.Count > 1 && list[1] is double d1 ? d1 : 0;
        var b = list.Count > 2 && list[2] is double d2 ? d2 : 0;
        return new GridLength(width(measured, a, b), GridUnitType.Star);
    }
}
