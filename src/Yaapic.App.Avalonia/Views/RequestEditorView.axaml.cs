using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Yaapic.Domain.Http;
using Yaapic.ViewModels.Requests;

namespace Yaapic.App.Avalonia.Views;

/// <summary>The request editor of one tab, bound to a <see cref="RequestEditorViewModel"/>.</summary>
public sealed partial class RequestEditorView : UserControl
{
    /// <summary>Creates the view.</summary>
    public RequestEditorView() => InitializeComponent();

    private RequestEditorViewModel? Editor => DataContext as RequestEditorViewModel;

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        var modifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        if (e.Key == Key.Enter && e.KeyModifiers == modifier && Editor?.SendCommand.CanExecute(null) == true)
        {
            Editor.SendCommand.Execute(null);
            e.Handled = true;
        }

        base.OnKeyDown(e);
    }

    private void OnMethodChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Editor is { } editor && MethodBox.SelectedIndex >= 0)
        {
            editor.Method = MethodBox.SelectedIndex == 1 ? HttpMethodKind.Post : HttpMethodKind.Get;
        }
    }

    private void OnContentTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Editor is { } editor && ContentTypeBox.SelectedIndex >= 0)
        {
            editor.BodyContentType = ContentTypeBox.SelectedIndex switch
            {
                0 => RawContentType.Json,
                1 => RawContentType.Text,
                _ => RawContentType.Xml,
            };
        }
    }

    private void OnBodyNone(object? sender, RoutedEventArgs e)
    {
        if (Editor is { } editor)
        {
            editor.BodyKind = BodyKind.None;
        }
    }

    private void OnBodyRaw(object? sender, RoutedEventArgs e)
    {
        if (Editor is { } editor)
        {
            editor.BodyKind = BodyKind.Raw;
        }
    }
}
