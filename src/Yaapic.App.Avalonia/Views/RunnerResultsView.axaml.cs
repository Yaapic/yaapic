using Avalonia;
using Avalonia.Controls;
using Yaapic.ViewModels.Runner;

namespace Yaapic.App.Avalonia.Views;

/// <summary>The right pane of the Runner window: progress, summary, filter, result rows and the details of the selected row.</summary>
public sealed partial class RunnerResultsView : UserControl
{
    private const double WideWidth = 1300;

    /// <summary>Initializes the view.</summary>
    public RunnerResultsView() => InitializeComponent();

    /// <inheritdoc/>
    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var wide = e.NewSize.Width > WideWidth;
        Grid.SetRow(Details, wide ? 0 : 1);
        Grid.SetColumn(Details, wide ? 1 : 0);
        Grid.SetRowSpan(RowsList, wide ? 2 : 1);
        Details.MinWidth = wide ? 320 : 0;
        Details.MaxWidth = wide ? 560 : double.PositiveInfinity;
    }

    private void OnFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedIndex: >= 0 and var index } && DataContext is RunnerViewModel vm)
        {
            vm.Filter = (RunnerFilter)index;
        }
    }

    private void OnRowSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox list && DataContext is RunnerViewModel vm)
        {
            vm.SelectedRow = list.SelectedItem as RunnerResultRowViewModel;
        }
    }
}
