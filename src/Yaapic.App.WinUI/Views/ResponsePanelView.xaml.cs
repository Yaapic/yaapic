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
        if (ViewModel is { } vm)
        {
            // Index based: boxed managed enums cannot cross into XAML under Native AOT.
            ViewModes.SelectedIndex = vm.ViewMode == BodyViewMode.Raw ? 1 : 0;
        }
    }

    private void OnViewModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is not null && ViewModes.SelectedIndex >= 0)
        {
            ViewModel.ViewMode = ViewModes.SelectedIndex == 1 ? BodyViewMode.Raw : BodyViewMode.Pretty;
        }
    }

    private void OnTabClick(object sender, RoutedEventArgs e)
    {
        var headers = ReferenceEquals(sender, HeadersTab);
        BodyTab.IsChecked = !headers;
        HeadersTab.IsChecked = headers;
        BodyPane.Visibility = headers ? Visibility.Collapsed : Visibility.Visible;
        HeadersPane.Visibility = headers ? Visibility.Visible : Visibility.Collapsed;
        HeadersBadge.Style = (Style)Application.Current.Resources[headers ? "YaapicCountBadgeActiveTextStyle" : "YaapicCountBadgeTextStyle"];
    }
}
