using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Yaapic.App.WinUI.Presentation;
using Yaapic.Domain.Http;
using Yaapic.ViewModels.Requests;

namespace Yaapic.App.WinUI.Views;

/// <summary>Editor of one request tab: name, method, URL, send, params, headers and body.</summary>
public sealed partial class RequestEditorView : UserControl
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(RequestEditorViewModel), typeof(RequestEditorView),
        new PropertyMetadata(null, (d, _) => ((RequestEditorView)d).OnViewModelChanged()));

    /// <summary>Raised when the user asks to save the request from the header button.</summary>
    public event RoutedEventHandler? SaveRequested;

    /// <summary>Initializes the view.</summary>
    public RequestEditorView() => InitializeComponent();

    private static readonly string[] ContentTypeNames = ["JSON", "Text", "XML"];

    /// <summary>Gets or sets the edited request.</summary>
    public RequestEditorViewModel? ViewModel
    {
        get => (RequestEditorViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private void OnViewModelChanged()
    {
        Bindings?.Update();
        if (ViewModel is not { } vm)
        {
            return;
        }

        // The pickers are index based: boxed managed enums cannot cross into XAML under Native AOT.
        BodyNone.IsChecked = vm.BodyKind != BodyKind.Raw;
        BodyRaw.IsChecked = vm.BodyKind == BodyKind.Raw;
        ContentTypeLabel.Text = ContentTypeNames[IndexOf(RequestEditorViewModel.ContentTypes, vm.BodyContentType)];
    }

    private void OnSaveClick(object sender, RoutedEventArgs e) => SaveRequested?.Invoke(this, e);

    private void OnCancelAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        // Escape only cancels a send in flight; otherwise it stays available to the focused control.
        if (ViewModel is { IsSending: true } vm && vm.CancelCommand.CanExecute(null))
        {
            vm.CancelCommand.Execute(null);
            args.Handled = true;
        }
    }

    private void OnUrlBarGotFocus(object sender, RoutedEventArgs e) => SetUrlBarFocused(true);

    private void OnUrlBarLostFocus(object sender, RoutedEventArgs e) => SetUrlBarFocused(false);

    private void SetUrlBarFocused(bool focused)
    {
        // A 2 px focus border replaces the 1 px one; the negative content margin keeps the text from moving.
        UrlBar.BorderBrush = (Brush)Application.Current.Resources[focused ? "YaapicFocusRingBrush" : "YaapicBorderControlBrush"];
        UrlBar.BorderThickness = new Thickness(focused ? 2 : 1);
        UrlBarContent.Margin = new Thickness(focused ? -1 : 0);
    }

    private void OnMethodClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.Method = ((MenuFlyoutItem)sender).Tag is "Post" ? HttpMethodKind.Post : HttpMethodKind.Get;
        }
    }

    private void OnContentTypeClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            var index = int.Parse((string)((MenuFlyoutItem)sender).Tag, System.Globalization.CultureInfo.InvariantCulture);
            ViewModel.BodyContentType = RequestEditorViewModel.ContentTypes[index];
            ContentTypeLabel.Text = ContentTypeNames[index];
        }
    }

    private void OnBodyKindChecked(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.BodyKind = ReferenceEquals(sender, BodyRaw) ? BodyKind.Raw : BodyKind.None;
        }
    }

    private void OnSendAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel?.SendCommand.CanExecute(null) == true)
        {
            ViewModel.SendCommand.Execute(null);
        }

        args.Handled = true;
    }

    private void OnTabClick(object sender, RoutedEventArgs e)
    {
        ParamsTab.IsChecked = ReferenceEquals(sender, ParamsTab);
        HeadersTab.IsChecked = ReferenceEquals(sender, HeadersTab);
        BodyTab.IsChecked = ReferenceEquals(sender, BodyTab);
        ParamsPane.Visibility = ParamsTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        HeadersPane.Visibility = HeadersTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        BodyPane.Visibility = BodyTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ParamsBadge.Style = BadgeStyle(ParamsTab);
        HeadersBadge.Style = BadgeStyle(HeadersTab);
        BodyBadge.Style = BadgeStyle(BodyTab);
    }

    private static Style BadgeStyle(ToggleButton tab) =>
        (Style)Application.Current.Resources[tab.IsChecked == true ? "YaapicCountBadgeActiveTextStyle" : "YaapicCountBadgeTextStyle"];

    private static int IndexOf<T>(IReadOnlyList<T> items, T value)
    {
        for (var i = 0; i < items.Count; i++)
        {
            if (EqualityComparer<T>.Default.Equals(items[i], value))
            {
                return i;
            }
        }

        return 0;
    }
}
