using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Yaapic.App.Avalonia.Presentation;
using Yaapic.Core.Variables;
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
        AddHandler(KeyDownEvent, OnSendShortcut, RoutingStrategies.Tunnel);
        foreach (var method in RequestEditorViewModel.Methods)
        {
            MethodBox.Items.Add(new ComboBoxItem { Content = method.ToText(), Foreground = BrushMap.Method(method) });
        }
    }

    /// <summary>Raised when the user clicks Save; the shell saves the active tab.</summary>
    public event EventHandler? SaveRequested;

    /// <summary>Raised when a variable popup asks to open the editor of a scope (the shell shows it).</summary>
    public event EventHandler<VariableScopeKind>? VariableScopeOpenRequested;

    private RequestEditorViewModel? Editor => DataContext as RequestEditorViewModel;

    private VariableContext? _variables;

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_variables is not null)
        {
            _variables.OpenRequested -= OnVariableOpenRequested;
        }

        _variables = Editor?.Variables;
        if (_variables is not null)
        {
            _variables.OpenRequested += OnVariableOpenRequested;
        }
    }

    private void OnVariableOpenRequested(object? sender, VariableScopeKind scope) => VariableScopeOpenRequested?.Invoke(this, scope);

    // Tunnelling: the code editor binds Ctrl+Enter itself (insert line below), so Send must see the key first.
    private void OnSendShortcut(object? sender, KeyEventArgs e)
    {
        var modifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        if (e.Key == Key.Enter && e.KeyModifiers == modifier && Editor?.SendCommand.CanExecute(null) == true)
        {
            Editor.SendCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnMethodChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Editor is { } editor && MethodBox.SelectedIndex >= 0)
        {
            editor.Method = RequestEditorViewModel.Methods[MethodBox.SelectedIndex];
            MethodBox.Foreground = BrushMap.Method(editor.Method);
        }
    }

    private void OnContentTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (Editor is { } editor && ContentTypeBox.SelectedIndex >= 0)
        {
            editor.BodyContentType = RequestEditorViewModel.ContentTypes[ContentTypeBox.SelectedIndex];
        }
    }

    private void OnBodyKindChanged(object? sender, SelectionChangedEventArgs e)
    {
        // Index based: the position is the one in RequestEditorViewModel.BodyKinds.
        if (Editor is { } editor && BodyKindBox.SelectedIndex >= 0)
        {
            editor.BodyKind = RequestEditorViewModel.BodyKinds[BodyKindBox.SelectedIndex];
        }
    }

    private void OnTimeoutChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (Editor is { } editor)
        {
            editor.TimeoutSeconds = (int)(e.NewValue ?? 0);
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e) => SaveRequested?.Invoke(this, EventArgs.Empty);
}
