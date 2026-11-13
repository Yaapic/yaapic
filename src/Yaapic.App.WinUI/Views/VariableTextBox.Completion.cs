using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.System;
using Yaapic.ViewModels.Requests;

namespace Yaapic.App.WinUI.Views;

// The completion list opened by typing {{ (or Ctrl+Space): the variables that resolve, then those of the other environments.
public sealed partial class VariableTextBox
{
    private Popup? _completion;
    private ListView? _suggestList;
    private IReadOnlyList<VariableSuggestion> _suggestions = [];
    private int _completionStart;

    private bool IsCompletionOpen => _completion?.IsOpen == true;

    private void UpdateCompletion()
    {
        if (Context is not { } context || IsReadOnly || XamlRoot is null || Input.SelectionLength > 0
            || Input.FocusState == FocusState.Unfocused
            || !VariableCompletion.TryGetPrefix(Input.Text, Input.SelectionStart, out var start, out var prefix))
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
        list.Items.Clear();
        foreach (var suggestion in suggestions)
        {
            list.Items.Add(CreateRow(suggestion));
        }

        list.SelectedIndex = 0;
        list.ScrollIntoView(list.SelectedItem);
        Place();
        _completion!.XamlRoot = XamlRoot;
        _completion.IsOpen = true;
    }

    private void EnsureCompletionPopup()
    {
        if (_completion is not null)
        {
            return;
        }

        _suggestList = new ListView
        {
            SelectionMode = ListViewSelectionMode.Single,
            IsTabStop = false,
            AllowFocusOnInteraction = false,
            IsItemClickEnabled = true,
            MaxHeight = 220,
            Width = 380,
            Padding = new Thickness(0),
        };
        _suggestList.ItemClick += (_, e) => AcceptCompletion(_suggestList.Items.IndexOf(e.ClickedItem));
        _completion = new Popup
        {
            IsLightDismissEnabled = false,
            Child = new Border
            {
                Background = Resolve("YaapicSurfaceElevatedBrush"),
                BorderBrush = Resolve("YaapicBorderOverlayBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Child = _suggestList,
            },
        };
        Host.Children.Add(_completion);
    }

    private ListViewItem CreateRow(VariableSuggestion suggestion)
    {
        var grid = new Grid { ColumnSpacing = 12, Opacity = suggestion.IsActive ? 1 : 0.6 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var name = new TextBlock
        {
            Text = suggestion.Name,
            FontFamily = (FontFamily)Application.Current.Resources["YaapicMonoFont"],
            Foreground = Resolve("YaapicTextPrimaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var preview = new TextBlock
        {
            Text = suggestion.IsSecret ? "••••" : suggestion.Preview,
            Foreground = Resolve("YaapicTextTertiaryBrush"),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var scope = new TextBlock
        {
            Text = suggestion.ScopeLabel,
            Foreground = Resolve("YaapicTextSecondaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(preview, 1);
        Grid.SetColumn(scope, 2);
        grid.Children.Add(name);
        grid.Children.Add(preview);
        grid.Children.Add(scope);
        return new ListViewItem { Content = grid, MinHeight = 30, AllowFocusOnInteraction = false, IsTabStop = false };
    }

    private void Place()
    {
        var left = Input.GetRectFromCharacterIndex(_completionStart, false);
        var origin = Host.TransformToVisual(null).TransformPoint(new Point(0, 0));
        var x = Math.Min(Math.Max(0, left.X), XamlRoot.Size.Width - 8 - 382 - origin.X);
        _completion!.HorizontalOffset = Math.Max(8 - origin.X, x);
        _completion.VerticalOffset = Host.ActualHeight + 4;
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

        var (text, caret) = VariableCompletion.Apply(Input.Text, Input.SelectionStart, _completionStart, _suggestions[index].Name);
        CloseCompletion();
        Input.Text = text;
        Input.SelectionStart = caret;
        Input.Focus(FocusState.Programmatic);
    }

    // Returns true when the key belongs to the list.
    private bool HandleCompletionKey(KeyRoutedEventArgs e)
    {
        var list = _suggestList;
        if (!IsCompletionOpen || list is null)
        {
            return false;
        }

        switch (e.Key)
        {
            case VirtualKey.Down:
                Select((list.SelectedIndex + 1) % list.Items.Count);
                break;
            case VirtualKey.Up:
                Select((list.SelectedIndex - 1 + list.Items.Count) % list.Items.Count);
                break;
            case VirtualKey.Enter or VirtualKey.Tab:
                AcceptCompletion(list.SelectedIndex);
                break;
            case VirtualKey.Escape:
                CloseCompletion();
                break;
            default:
                return false;
        }

        e.Handled = true;
        return true;

        void Select(int index)
        {
            list.SelectedIndex = index;
            list.ScrollIntoView(list.SelectedItem);
        }
    }

    // Ctrl+Space: opens the list at the caret, writing the braces first when the caret is not in a placeholder.
    private void OpenCompletionManually()
    {
        if (Context is null || IsReadOnly)
        {
            return;
        }

        if (!VariableCompletion.TryGetPrefix(Input.Text, Input.SelectionStart, out _, out _))
        {
            var caret = Input.SelectionStart;
            Input.Text = Input.Text.Insert(caret, "{{");
            Input.SelectionStart = caret + 2;
        }

        UpdateCompletion();
    }
}
