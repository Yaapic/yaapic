using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Yaapic.App.Avalonia.Presentation;
using Yaapic.Domain.Http;
using Yaapic.ViewModels.Requests;

namespace Yaapic.App.Avalonia.Views;

/// <summary>The request editor of one tab, bound to a <see cref="RequestEditorViewModel"/>.</summary>
public sealed partial class RequestEditorView : UserControl
{
    /// <summary>Creates the view.</summary>
    public RequestEditorView()
    {
        InitializeComponent();
        foreach (var method in RequestEditorViewModel.Methods)
        {
            MethodBox.Items.Add(new ComboBoxItem { Content = method.ToText(), Foreground = BrushMap.Method(method) });
        }
    }

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
            editor.Method = RequestEditorViewModel.Methods[MethodBox.SelectedIndex];
        }
    }

    private void OnContentTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Editor is { } editor && ContentTypeBox.SelectedIndex >= 0)
        {
            editor.BodyContentType = RequestEditorViewModel.ContentTypes[ContentTypeBox.SelectedIndex];
        }
    }

    private void OnBodyKindClick(object? sender, RoutedEventArgs e)
    {
        // Index based: the Tag holds the position in RequestEditorViewModel.BodyKinds.
        if (Editor is { } editor && sender is RadioButton { Tag: string index })
        {
            editor.BodyKind = RequestEditorViewModel.BodyKinds[int.Parse(index, CultureInfo.InvariantCulture)];
        }
    }
}
