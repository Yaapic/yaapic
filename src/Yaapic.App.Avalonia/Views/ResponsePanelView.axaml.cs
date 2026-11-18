using Avalonia.Controls;
using Yaapic.Core.Responses;
using Yaapic.ViewModels.Responses;

namespace Yaapic.App.Avalonia.Views;

/// <summary>Shows the response of a request: status, time, size, body and headers, or the error.</summary>
public sealed partial class ResponsePanelView : UserControl
{
    /// <summary>Creates the view.</summary>
    public ResponsePanelView() => InitializeComponent();

    private ResponsePanelViewModel? _diffSource;

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        _diffSource?.ShowDiffRequested -= OnShowDiffRequested;
        _diffSource = DataContext as ResponsePanelViewModel;
        _diffSource?.ShowDiffRequested += OnShowDiffRequested;
    }

    // The Diff tab is the last one of the panel.
    private void OnShowDiffRequested(object? sender, EventArgs e) => PanelTabs.SelectedIndex = PanelTabs.ItemCount - 1;

    private void OnDiffCompareChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox { SelectedIndex: >= 0 and var index } && DataContext is ResponsePanelViewModel vm)
        {
            vm.Diff.UseBaseline = index == 1;
        }
    }

    private void OnTestFilterChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedIndex: >= 0 and var index } && DataContext is ResponsePanelViewModel vm)
        {
            vm.Tests.Filter = index == 1 ? TestFilter.Failed : TestFilter.All;
        }
    }

    private void OnViewModeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox { SelectedIndex: >= 0 and var index } && DataContext is ResponsePanelViewModel vm)
        {
            vm.ViewMode = index == 1 ? BodyViewMode.Raw : BodyViewMode.Pretty;
        }
    }
}
