using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Yaapic.ViewModels.Requests;

namespace Yaapic.App.Avalonia.Controls;

// The completion list opened by typing {{ (or Ctrl+Space): the variables that resolve, then those of the other environments.
public sealed partial class VariableTextBox
{
    private Popup? _completion;
    private ListBox? _suggestList;
    private IReadOnlyList<VariableSuggestion> _suggestions = [];
    private int _completionStart;

    private bool IsCompletionOpen => _completion?.IsOpen == true;

    private IBrush Themed(string key) =>
        this.TryFindResource(key, ActualThemeVariant, out var value) && value is IBrush brush ? brush : Brushes.Gray;

    private void StartCompletion()
    {
        // Tunnel: the list keys must be seen before the text box moves its caret or handles Tab.
        Input.AddHandler(KeyDownEvent, OnCompletionKeyDown, global::Avalonia.Interactivity.RoutingStrategies.Tunnel);
        Input.PropertyChanged += (_, e) =>
        {
            if (e.Property == TextBox.CaretIndexProperty && IsCompletionOpen)
            {
                UpdateCompletion();
            }
        };
    }

    private void UpdateCompletion()
    {
        if (Context is not { } context || Input.IsReadOnly || !Input.IsKeyboardFocusWithin || Input.SelectionStart != Input.SelectionEnd
            || !VariableCompletion.TryGetPrefix(Input.Text, Input.CaretIndex, out var start, out var prefix))
        {
            CloseCompletion();
            return;
        }

        var suggestions = context.Suggest(prefix);
        if (suggestions.Count == 0)
        {
            CloseCompletion();
            return;
        }

        _suggestions = suggestions;
        _completionStart = start;
        EnsureCompletionPopup();
        var list = _suggestList!;
        var rows = new List<ListBoxItem>(suggestions.Count);
        for (var i = 0; i < suggestions.Count; i++)
        {
            rows.Add(CreateRow(suggestions[i], i));
        }

        list.ItemsSource = rows;
        list.SelectedIndex = 0;
        list.ScrollIntoView(0);
        PlaceCompletion();
        _completion!.IsOpen = true;
    }

    private void EnsureCompletionPopup()
    {
        if (_completion is not null)
        {
            return;
        }

        _suggestList = new ListBox { Focusable = false, MaxHeight = 220, Width = 380, Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
        _completion = new Popup
        {
            PlacementTarget = Host,
            Placement = PlacementMode.BottomEdgeAlignedLeft,
            IsLightDismissEnabled = false,
            Child = new Border
            {
                Background = Themed("YaapicSurfaceElevatedBrush"),
                BorderBrush = Themed("YaapicBorderOverlayBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Child = _suggestList,
            },
        };
        Host.Children.Add(_completion);
    }

    private ListBoxItem CreateRow(VariableSuggestion suggestion, int index)
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto"), Opacity = suggestion.IsActive ? 1 : 0.6 };
        var name = new TextBlock
        {
            Text = suggestion.Name,
            FontFamily = this.TryFindResource("YaapicMonoFont", ActualThemeVariant, out var font) && font is FontFamily family ? family : FontFamily.Default,
            Foreground = Themed("YaapicTextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var preview = new TextBlock
        {
            Text = suggestion.IsSecret ? "••••" : suggestion.Preview,
            Foreground = Themed("YaapicTextTertiaryBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            Margin = new Thickness(12, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var scope = new TextBlock
        {
            Text = suggestion.ScopeLabel,
            Foreground = Themed("YaapicTextSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(preview, 1);
        Grid.SetColumn(scope, 2);
        grid.Children.Add(name);
        grid.Children.Add(preview);
        grid.Children.Add(scope);
        var row = new ListBoxItem { Content = grid, Focusable = false, MinHeight = 28 };
        row.Tapped += (_, _) => AcceptCompletion(index);
        return row;
    }

    private void PlaceCompletion()
    {
        var x = 0.0;
        if (_presenter?.TextLayout is { } layout)
        {
            var rect = layout.HitTestTextPosition(_completionStart);
            x = _presenter.TranslatePoint(rect.TopLeft, Host)?.X ?? 0;
        }

        _completion!.HorizontalOffset = Math.Max(0, x);
        _completion.VerticalOffset = 4;
    }

    private void CloseCompletion()
    {
        if (_completion is not null)
        {
            _completion.IsOpen = false;
        }
    }

    private void AcceptCompletion(int index)
    {
        if (index < 0 || index >= _suggestions.Count)
        {
            return;
        }

        var (text, caret) = VariableCompletion.Apply(Input.Text ?? "", Input.CaretIndex, _completionStart, _suggestions[index].Name);
        CloseCompletion();
        Input.Text = text;
        Input.CaretIndex = caret;
        Input.Focus();
    }

    private void OnCompletionKeyDown(object? sender, KeyEventArgs e)
    {
        var modifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        if (e.Key == Key.Space && e.KeyModifiers == modifier)
        {
            OpenCompletionManually();
            e.Handled = true;
            return;
        }

        var list = _suggestList;
        if (!IsCompletionOpen || list is null || list.ItemCount == 0)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                Select((list.SelectedIndex + 1) % list.ItemCount);
                break;
            case Key.Up:
                Select((list.SelectedIndex - 1 + list.ItemCount) % list.ItemCount);
                break;
            case Key.Enter or Key.Tab:
                AcceptCompletion(list.SelectedIndex);
                break;
            case Key.Escape:
                CloseCompletion();
                break;
            default:
                return;
        }

        e.Handled = true;

        void Select(int index)
        {
            list.SelectedIndex = index;
            list.ScrollIntoView(index);
        }
    }

    // Ctrl+Space: opens the list at the caret, writing the braces first when the caret is not in a placeholder.
    private void OpenCompletionManually()
    {
        if (Context is null || Input.IsReadOnly)
        {
            return;
        }

        if (!VariableCompletion.TryGetPrefix(Input.Text, Input.CaretIndex, out _, out _))
        {
            var caret = Input.CaretIndex;
            Input.Text = (Input.Text ?? "").Insert(caret, "{{");
            Input.CaretIndex = caret + 2;
        }

        UpdateCompletion();
    }
}
