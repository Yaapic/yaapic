using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Yaapic.Localization;

namespace Yaapic.App.WinUI.Presentation;

/// <summary>Waterfall helpers used through <c>x:Bind</c> function binding: the bar is placed with star columns, so it stays proportional on resize.</summary>
public static class TimingMap
{
    /// <summary>Gets the width of the empty lead before the bar.</summary>
    /// <param name="measured">Whether the phase was measured.</param>
    /// <param name="offset">The offset fraction.</param>
    /// <returns>A star width equal to the row's offset fraction.</returns>
    public static GridLength Lead(bool measured, double offset) => new(measured ? Math.Clamp(offset, 0, 1) : 0, GridUnitType.Star);

    /// <summary>Gets the width of the bar.</summary>
    /// <param name="measured">Whether the phase was measured.</param>
    /// <param name="fraction">The duration fraction.</param>
    /// <returns>A star width equal to the row's fraction.</returns>
    public static GridLength Bar(bool measured, double fraction) => new(measured ? Math.Clamp(fraction, 0, 1) : 0, GridUnitType.Star);

    /// <summary>Gets the width of the empty rest after the bar.</summary>
    /// <param name="measured">Whether the phase was measured.</param>
    /// <param name="offset">The offset fraction.</param>
    /// <param name="fraction">The duration fraction.</param>
    /// <returns>A star width for what remains of the track; the full track for an unmeasured row.</returns>
    public static GridLength Rest(bool measured, double offset, double fraction) =>
        new(measured ? Math.Max(0, 1 - Math.Clamp(offset, 0, 1) - Math.Clamp(fraction, 0, 1)) : 1, GridUnitType.Star);

    /// <summary>Gets the bar brush: the accent for the Waiting phase, a neutral token otherwise.</summary>
    /// <param name="label">The phase label.</param>
    /// <returns>The brush.</returns>
    public static Brush BarBrush(string label) => (Brush)Application.Current.Resources[
        label == Strings.TimingWaiting ? "YaapicAccentFillBrush" : "YaapicBorderOverlayBrush"];

    /// <summary>Gets the visibility of the bar: hidden for an unmeasured row.</summary>
    /// <param name="measured">Whether the phase was measured.</param>
    /// <returns>The visibility.</returns>
    public static Visibility WhenMeasured(bool measured) => measured ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Gets the visibility of the muted track: shown for an unmeasured row only.</summary>
    /// <param name="measured">Whether the phase was measured.</param>
    /// <returns>The visibility.</returns>
    public static Visibility WhenUnmeasured(bool measured) => measured ? Visibility.Collapsed : Visibility.Visible;
}
