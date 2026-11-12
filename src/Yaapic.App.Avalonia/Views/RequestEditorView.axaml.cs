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
        Sections.SizeChanged += (_, _) => UpdateSectionsOverflow();
        Sections.SelectionChanged += (_, _) => UpdateSectionsOverflowLabel();
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
    private PaneLayoutViewModel? _layout;

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

        if (_layout is not null)
        {
            _layout.PropertyChanged -= OnLayoutChanged;
        }

        _layout = Editor?.Layout;
        if (_layout is not null)
        {
            _layout.PropertyChanged += OnLayoutChanged;
        }

        ApplyLayout();
    }

    private double _stripWidth;
    private bool _compactSections;

    // Swaps the tab strip for a drop-down when the tabs are wider than the room (a narrow request column).
    // The width is summed while the strip is shown and kept, so the swap back needs no hidden measure.
    private void UpdateSectionsOverflow()
    {
        // The strip is one row of tabs while it fits: its right edge is then its natural width. Once the tabs wrap, keep the last good value.
        var tabs = Sections.Items.OfType<TabItem>().ToList();
        if (!_compactSections && tabs.Count > 0 && tabs.All(t => t.Bounds.Width > 0 && t.Bounds.Y == tabs[0].Bounds.Y))
        {
            _stripWidth = tabs.Max(t => t.Bounds.Right);
        }

        if (Sections.Bounds.Width <= 0 || _stripWidth <= 0)
        {
            return;
        }

        _compactSections = _stripWidth + 16 > Sections.Bounds.Width;
        Sections.Classes.Set("compact", _compactSections);
        Sections.Margin = new global::Avalonia.Thickness(0, _compactSections ? 32 : 0, 0, 0);
        SectionsOverflow.IsVisible = _compactSections;
        UpdateSectionsOverflowLabel();
    }

    private static string Label(TabItem tab) =>
        (tab.Header as StackPanel)?.Children.OfType<TextBlock>().FirstOrDefault()?.Text ?? tab.Header?.ToString() ?? "";

    private void UpdateSectionsOverflowLabel() =>
        SectionsOverflowLabel.Text = Sections.SelectedItem is TabItem tab ? Label(tab) : "";

    private void OnSectionsOverflowOpening(object? sender, EventArgs e)
    {
        var menu = (MenuFlyout)SectionsOverflowButton.Flyout!;
        menu.Items.Clear();
        foreach (var tab in Sections.Items.OfType<TabItem>())
        {
            var item = new MenuItem { Header = Label(tab), ToggleType = MenuItemToggleType.Radio, GroupName = "sections", IsChecked = ReferenceEquals(tab, Sections.SelectedItem) };
            item.Click += (_, _) => Sections.SelectedItem = tab;
            menu.Items.Add(item);
        }
    }

    private void OnLayoutChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => ApplyLayout();

    // Stacked: everything spans the three columns and the response is in row 4. Side by side: the header and URL bar keep the full width,
    // the sections take the left column, and the splitter and response sit in the other two columns next to them.
    private void ApplyLayout()
    {
        var horizontal = _layout?.IsHorizontal == true;
        var columns = Panes.ColumnDefinitions;
        var rows = Panes.RowDefinitions;
        columns[0].Width = new GridLength(1, GridUnitType.Star);
        columns[0].MinWidth = horizontal ? 280 : 0;
        columns[1].Width = horizontal ? GridLength.Auto : new GridLength(0);
        columns[2].Width = horizontal ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
        columns[2].MinWidth = horizontal ? 280 : 0;
        rows[2].Height = horizontal ? new GridLength(1, GridUnitType.Star) : new GridLength(9, GridUnitType.Star);
        rows[3].Height = GridLength.Auto;
        rows[4].Height = horizontal ? new GridLength(0) : new GridLength(11, GridUnitType.Star);
        rows[4].MinHeight = horizontal ? 0 : 180;
        Grid.SetColumnSpan(Sections, horizontal ? 1 : 3);
        Grid.SetRow(Splitter, horizontal ? 2 : 3);
        Grid.SetColumn(Splitter, horizontal ? 1 : 0);
        Grid.SetColumnSpan(Splitter, horizontal ? 1 : 3);
        Splitter.Classes.Set("rows", !horizontal);
        Splitter.Classes.Set("columns", horizontal);
        Splitter.ResizeDirection = horizontal ? GridResizeDirection.Columns : GridResizeDirection.Rows;
        Grid.SetRow(ResponseView, horizontal ? 2 : 4);
        Grid.SetColumn(ResponseView, horizontal ? 2 : 0);
        Grid.SetColumnSpan(ResponseView, horizontal ? 1 : 3);
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
