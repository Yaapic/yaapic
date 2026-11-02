using Avalonia.Controls;
using Avalonia.Interactivity;
using Yaapic.Core.Responses;
using Yaapic.ViewModels.Responses;

namespace Yaapic.App.Avalonia.Views;

/// <summary>Shows the response of a request: status, time, size, body and headers, or the error.</summary>
public sealed partial class ResponsePanelView : UserControl
{
    /// <summary>Creates the view.</summary>
    public ResponsePanelView() => InitializeComponent();

    private void OnPretty(object? sender, RoutedEventArgs e) => SetMode(BodyViewMode.Pretty);

    private void OnRaw(object? sender, RoutedEventArgs e) => SetMode(BodyViewMode.Raw);

    private void SetMode(BodyViewMode mode)
    {
        if (DataContext is ResponsePanelViewModel vm)
        {
            vm.ViewMode = mode;
        }
    }
}
