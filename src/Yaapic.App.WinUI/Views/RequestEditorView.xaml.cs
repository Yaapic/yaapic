using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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

    /// <summary>Initializes the view.</summary>
    public RequestEditorView() => InitializeComponent();

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
        BodyKinds.SelectedIndex = vm.BodyKind == BodyKind.Raw ? 1 : 0;
        MethodBox.SelectedIndex = IndexOf(RequestEditorViewModel.Methods, vm.Method);
        ContentTypeBox.SelectedIndex = IndexOf(RequestEditorViewModel.ContentTypes, vm.BodyContentType);
    }

    private void OnSendAccelerator(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        if (ViewModel?.SendCommand.CanExecute(null) == true)
        {
            ViewModel.SendCommand.Execute(null);
        }

        args.Handled = true;
    }

    private void OnTabChanged(SelectorBar sender, SelectorBarSelectionChangedEventArgs args)
    {
        // Fires during InitializeComponent, before the panes below the selector are created.
        if (BodyPane is null)
        {
            return;
        }

        var index = sender.Items.IndexOf(sender.SelectedItem);
        ParamsPane.Visibility = index == 0 ? Visibility.Visible : Visibility.Collapsed;
        HeadersPane.Visibility = index == 1 ? Visibility.Visible : Visibility.Collapsed;
        BodyPane.Visibility = index == 2 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnBodyKindChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is not null && BodyKinds.SelectedIndex >= 0)
        {
            ViewModel.BodyKind = BodyKinds.SelectedIndex == 1 ? BodyKind.Raw : BodyKind.None;
        }
    }

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

    private void OnMethodChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is not null && MethodBox.SelectedIndex >= 0)
        {
            var method = RequestEditorViewModel.Methods[MethodBox.SelectedIndex];
            ViewModel.Method = method;
            MethodBox.Foreground = BrushMap.Method(method);
        }
    }

    private void OnContentTypeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ViewModel is not null && ContentTypeBox.SelectedIndex >= 0)
        {
            ViewModel.BodyContentType = RequestEditorViewModel.ContentTypes[ContentTypeBox.SelectedIndex];
        }
    }
}
