using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Yaapic.ViewModels.LoadTesting;

namespace Yaapic.App.Avalonia.Controls;

/// <summary>A small line chart: one accent polyline over two horizontal grid lines, X and Y normalized to 0..1.</summary>
public sealed class LineChart : Control
{
    /// <summary>Identifies the <see cref="Points"/> property.</summary>
    public static readonly StyledProperty<IReadOnlyList<ChartPoint>?> PointsProperty =
        AvaloniaProperty.Register<LineChart, IReadOnlyList<ChartPoint>?>(nameof(Points));

    /// <summary>Identifies the <see cref="Title"/> property.</summary>
    public static readonly StyledProperty<string?> TitleProperty = AvaloniaProperty.Register<LineChart, string?>(nameof(Title));

    /// <summary>Identifies the <see cref="MaxLabel"/> property.</summary>
    public static readonly StyledProperty<string?> MaxLabelProperty = AvaloniaProperty.Register<LineChart, string?>(nameof(MaxLabel));

    private const double HeaderHeight = 22;

    static LineChart() => AffectsRender<LineChart>(PointsProperty, TitleProperty, MaxLabelProperty);

    /// <summary>Gets or sets the points to draw; an empty or missing list draws nothing.</summary>
    public IReadOnlyList<ChartPoint>? Points
    {
        get => GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    /// <summary>Gets or sets the title shown above the plot.</summary>
    public string? Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets the label of the largest value, shown at the top right.</summary>
    public string? MaxLabel
    {
        get => GetValue(MaxLabelProperty);
        set => SetValue(MaxLabelProperty, value);
    }

    /// <inheritdoc />
    public override void Render(DrawingContext context)
    {
        var accent = Brush("YaapicAccentFillBrush");
        var subtle = Brush("YaapicBorderSubtleBrush");
        var primary = Brush("YaapicTextPrimaryBrush");
        var secondary = Brush("YaapicTextSecondaryBrush");
        var plot = new Rect(0, HeaderHeight, Bounds.Width, Math.Max(0, Bounds.Height - HeaderHeight));
        if (!string.IsNullOrEmpty(Title))
        {
            context.DrawText(Text(Title, primary), new Point(0, 0));
        }

        if (!string.IsNullOrEmpty(MaxLabel))
        {
            var max = Text(MaxLabel, secondary);
            context.DrawText(max, new Point(Math.Max(0, Bounds.Width - max.Width), 0));
        }

        var border = new Pen(subtle, 1);
        context.DrawRectangle(null, border, plot, 4, 4);
        context.DrawLine(border, new Point(0, plot.Top + plot.Height * 0.25), new Point(plot.Right, plot.Top + plot.Height * 0.25));
        context.DrawLine(border, new Point(0, plot.Top + plot.Height * 0.5), new Point(plot.Right, plot.Top + plot.Height * 0.5));
        if (Points is not { Count: > 1 } points)
        {
            return;
        }

        var geometry = new StreamGeometry();
        using (var g = geometry.Open())
        {
            for (var i = 0; i < points.Count; i++)
            {
                // Y = 1 is the largest value and is drawn at the top.
                var p = new Point(plot.Left + points[i].X * plot.Width, plot.Top + (1 - points[i].Y) * plot.Height);
                if (i == 0)
                {
                    g.BeginFigure(p, false);
                }
                else
                {
                    g.LineTo(p);
                }
            }

            g.EndFigure(false);
        }

        context.DrawGeometry(null, new Pen(accent, 1.5, lineJoin: PenLineJoin.Round), geometry);
    }

    private IBrush Brush(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Gray;

    private static FormattedText Text(string text, IBrush brush) =>
        new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Typeface.Default, 12, brush);
}
