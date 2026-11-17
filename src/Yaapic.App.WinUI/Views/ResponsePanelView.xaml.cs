using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.Core.Responses;
using Yaapic.ViewModels.Responses;

namespace Yaapic.App.WinUI.Views;

/// <summary>Shows the response of a request: status, time, size, body and headers, or the error.</summary>
public sealed partial class ResponsePanelView : UserControl
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(ResponsePanelViewModel), typeof(ResponsePanelView),
        new PropertyMetadata(null, (d, _) => ((ResponsePanelView)d).OnViewModelChanged()));

    private ResponsePanelViewModel? _diffSource;

    /// <summary>Initializes the view.</summary>
    public ResponsePanelView() => InitializeComponent();

    /// <summary>Gets or sets the displayed response.</summary>
    public ResponsePanelViewModel? ViewModel
    {
        get => (ResponsePanelViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private void OnViewModelChanged()
    {
        Bindings?.Update();
        _diffSource?.ShowDiffRequested -= OnShowDiffRequested;
        _diffSource = ViewModel;
        if (ViewModel is { } vm)
        {
            vm.ShowDiffRequested += OnShowDiffRequested;
            DiffCompare.SelectedIndex = vm.Diff.UseBaseline ? 1 : 0;
            // Index based: boxed managed enums cannot cross into XAML under Native AOT.
            ViewModes.SelectedIndex = vm.ViewMode == BodyViewMode.Raw ? 1 : 0;
            TestFilters.SelectedIndex = vm.Tests.Filter == TestFilter.Failed ? 1 : 0;
        }
    }

    private void OnViewModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is not null && ViewModes.SelectedIndex >= 0)
        {
            ViewModel.ViewMode = ViewModes.SelectedIndex == 1 ? BodyViewMode.Raw : BodyViewMode.Pretty;
        }
    }

    private void OnTestFilterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is not null && TestFilters.SelectedIndex >= 0)
        {
            ViewModel.Tests.Filter = TestFilters.SelectedIndex == 1 ? TestFilter.Failed : TestFilter.All;
        }
    }

    private void OnTabClick(object sender, RoutedEventArgs e)
    {
        var headers = ReferenceEquals(sender, HeadersTab);
        var tests = ReferenceEquals(sender, TestsTab);
        var timing = ReferenceEquals(sender, TimingTab);
        ShowTab(headers, tests, timing, ReferenceEquals(sender, DiffTab));
    }

    private void OnShowDiffRequested(object? sender, EventArgs e) => ShowTab(false, false, false, true);

    private void OnDiffCompareChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is not null && DiffCompare.SelectedIndex >= 0)
        {
            ViewModel.Diff.UseBaseline = DiffCompare.SelectedIndex == 1;
        }
    }

    private void ShowTab(bool headers, bool tests, bool timing, bool diff)
    {
        BodyTab.IsChecked = !headers && !tests && !timing && !diff;
        DiffTab.IsChecked = diff;
        DiffPane.Visibility = diff ? Visibility.Visible : Visibility.Collapsed;
        HeadersTab.IsChecked = headers;
        TestsTab.IsChecked = tests;
        TimingTab.IsChecked = timing;
        TimingPane.Visibility = timing ? Visibility.Visible : Visibility.Collapsed;
        BodyPane.Visibility = headers || tests || timing || diff ? Visibility.Collapsed : Visibility.Visible;
        HeadersPane.Visibility = headers ? Visibility.Visible : Visibility.Collapsed;
        TestsPane.Visibility = tests ? Visibility.Visible : Visibility.Collapsed;
        HeadersBadge.Style = (Style)Application.Current.Resources[headers ? "YaapicCountBadgeActiveTextStyle" : "YaapicCountBadgeTextStyle"];
    }
}
