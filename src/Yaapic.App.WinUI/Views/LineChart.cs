using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Windows.Foundation;
using Yaapic.ViewModels.LoadTesting;

namespace Yaapic.App.WinUI.Views;

/// <summary>A small line chart: one accent polyline over two horizontal grid lines, X and Y normalized to 0..1.</summary>
public sealed partial class LineChart : UserControl
{
    /// <summary>Identifies the <see cref="Points"/> dependency property.</summary>
    public static readonly DependencyProperty PointsProperty = DependencyProperty.Register(
        nameof(Points), typeof(IReadOnlyList<ChartPoint>), typeof(LineChart),
        new PropertyMetadata(null, (d, _) => ((LineChart)d).Redraw()));

    /// <summary>Identifies the <see cref="Title"/> dependency property.</summary>
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(LineChart), new PropertyMetadata(string.Empty, (d, _) => ((LineChart)d)._title.Text = ((LineChart)d).Title));

    /// <summary>Identifies the <see cref="MaxLabel"/> dependency property.</summary>
    public static readonly DependencyProperty MaxLabelProperty = DependencyProperty.Register(
        nameof(MaxLabel), typeof(string), typeof(LineChart), new PropertyMetadata(string.Empty, (d, _) => ((LineChart)d)._max.Text = ((LineChart)d).MaxLabel));

    private readonly Border _frame = new();
    private readonly Canvas _plot = new();
    private readonly Polyline _line = new() { StrokeThickness = 1.5, StrokeLineJoin = PenLineJoin.Round };
    private readonly Line _gridTop = new() { StrokeThickness = 1 };
    private readonly Line _gridMiddle = new() { StrokeThickness = 1 };
    private readonly TextBlock _title = new();
    private readonly TextBlock _max = new();

    /// <summary>Initializes the chart.</summary>
    public LineChart()
    {
        var resources = Application.Current.Resources;
        _title.Style = _max.Style = (Style)resources["YaapicBodyStrongTextStyle"];
        _max.HorizontalAlignment = HorizontalAlignment.Right;
        _plot.Children.Add(_gridTop);
        _plot.Children.Add(_gridMiddle);
        _plot.Children.Add(_line);
        var header = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = GridLength.Auto } } };
        Grid.SetColumn(_max, 1);
        header.Children.Add(_title);
        header.Children.Add(_max);
        _frame.BorderThickness = new Thickness(1);
        _frame.CornerRadius = new CornerRadius(4);
        _frame.Child = _plot;
        Grid.SetRow(_frame, 1);
        Content = new Grid
        {
            RowSpacing = 6,
            RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition() },
            Children = { header, _frame },
        };
        _plot.SizeChanged += (_, _) => Redraw();
        Loaded += (_, _) => ApplyBrushes();
        ActualThemeChanged += (_, _) => ApplyBrushes();
    }

    /// <summary>Gets or sets the points to draw; an empty or missing list draws nothing.</summary>
    public IReadOnlyList<ChartPoint>? Points
    {
        get => (IReadOnlyList<ChartPoint>?)GetValue(PointsProperty);
        set => SetValue(PointsProperty, value);
    }

    /// <summary>Gets or sets the title shown above the plot.</summary>
    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    /// <summary>Gets or sets the label of the largest value, shown at the top right.</summary>
    public string MaxLabel
    {
        get => (string)GetValue(MaxLabelProperty);
        set => SetValue(MaxLabelProperty, value);
    }

    // Code lookups only see the application theme, so the brushes come from the dictionary of the displayed theme.
    private void ApplyBrushes()
    {
        var light = ActualTheme == ElementTheme.Light;
        _line.Stroke = Presentation.BrushMap.Themed("YaapicAccentFillBrush", light);
        _gridTop.Stroke = _gridMiddle.Stroke = _frame.BorderBrush = Presentation.BrushMap.Themed("YaapicBorderSubtleBrush", light);
        _max.Foreground = Presentation.BrushMap.Themed("YaapicTextSecondaryBrush", light);
    }

    private void Redraw()
    {
        var width = _plot.ActualWidth;
        var height = _plot.ActualHeight;
        _gridTop.X1 = _gridMiddle.X1 = 0;
        _gridTop.X2 = _gridMiddle.X2 = width;
        _gridTop.Y1 = _gridTop.Y2 = height * 0.25;
        _gridMiddle.Y1 = _gridMiddle.Y2 = height * 0.5;
        var points = new PointCollection();
        if (Points is { Count: > 0 } source)
        {
            foreach (var point in source)
            {
                // Y = 1 is the largest value and is drawn at the top.
                points.Add(new Point(point.X * width, (1 - point.Y) * height));
            }
        }

        _line.Points = points;
    }
}
