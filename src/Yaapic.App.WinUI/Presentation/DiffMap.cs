using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Yaapic.Core.Diff;

namespace Yaapic.App.WinUI.Presentation;

/// <summary>Diff row helpers used through <c>x:Bind</c> function binding.</summary>
public static class DiffMap
{
    /// <summary>Gets the row background: the success surface for an added line, the danger surface for a removed one.</summary>
    /// <param name="op">The line operation.</param>
    /// <returns>The brush from the application resources, or a transparent brush for an equal line.</returns>
    public static Brush Background(DiffOp op) => op switch
    {
        DiffOp.Added => (Brush)Application.Current.Resources["YaapicSuccessBackgroundBrush"],
        DiffOp.Removed => (Brush)Application.Current.Resources["YaapicDangerBackgroundBrush"],
        _ => new SolidColorBrush(Microsoft.UI.Colors.Transparent),
    };

    /// <summary>Gets the one-character marker of a line.</summary>
    /// <param name="op">The line operation.</param>
    /// <param name="isFold">Whether the row is a fold.</param>
    /// <returns><c>+</c>, <c>-</c> or an empty string.</returns>
    public static string Marker(DiffOp op, bool isFold) => isFold ? "" : op switch
    {
        DiffOp.Added => "+",
        DiffOp.Removed => "-",
        _ => "",
    };

    /// <summary>Gets the visibility of the line cells: hidden for a fold row.</summary>
    /// <param name="isFold">Whether the row is a fold.</param>
    /// <returns>The visibility.</returns>
    public static Visibility WhenLine(bool isFold) => isFold ? Visibility.Collapsed : Visibility.Visible;

    /// <summary>Gets the visibility of the fold text: shown for a fold row only.</summary>
    /// <param name="isFold">Whether the row is a fold.</param>
    /// <returns>The visibility.</returns>
    public static Visibility WhenFold(bool isFold) => isFold ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Gets the visibility of the comparison content: shown when a comparison exists and nothing makes it unavailable.</summary>
    /// <param name="hasComparison">Whether two snapshots are compared.</param>
    /// <param name="reason">The unavailable reason, if any.</param>
    /// <returns>The visibility.</returns>
    public static Visibility WhenComparable(bool hasComparison, string? reason) =>
        hasComparison && string.IsNullOrEmpty(reason) ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Gets the visibility of the empty state: shown when there is nothing to compare and no reason to show.</summary>
    /// <param name="hasComparison">Whether two snapshots are compared.</param>
    /// <param name="reason">The unavailable reason, if any.</param>
    /// <returns>The visibility.</returns>
    public static Visibility WhenNeedTwo(bool hasComparison, string? reason) =>
        !hasComparison && string.IsNullOrEmpty(reason) ? Visibility.Visible : Visibility.Collapsed;
}
