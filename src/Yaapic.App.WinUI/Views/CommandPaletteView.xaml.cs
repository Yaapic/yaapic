using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Windows.System;
using Yaapic.ViewModels.Palette;

namespace Yaapic.App.WinUI.Views;

/// <summary>The Ctrl+K command palette overlay, bound to a <see cref="CommandPaletteViewModel"/>.</summary>
public sealed partial class CommandPaletteView : UserControl
{
    /// <summary>Identifies the <see cref="ViewModel"/> dependency property.</summary>
    public static readonly DependencyProperty ViewModelProperty = DependencyProperty.Register(
        nameof(ViewModel), typeof(CommandPaletteViewModel), typeof(CommandPaletteView),
        new PropertyMetadata(null, (d, e) => ((CommandPaletteView)d).OnViewModelChanged(
            (CommandPaletteViewModel?)e.OldValue, (CommandPaletteViewModel?)e.NewValue)));

    private UIElement? _previousFocus;

    /// <summary>Initializes the view.</summary>
    public CommandPaletteView() => InitializeComponent();

    /// <summary>Gets or sets the palette shown.</summary>
    public CommandPaletteViewModel? ViewModel
    {
        get => (CommandPaletteViewModel?)GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    private void OnViewModelChanged(CommandPaletteViewModel? oldValue, CommandPaletteViewModel? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.PropertyChanged -= OnPaletteChanged;
        }

        if (newValue is not null)
        {
            newValue.PropertyChanged += OnPaletteChanged;
        }

        Bindings.Update();
    }

    private void OnPaletteChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        if (e.PropertyName == nameof(CommandPaletteViewModel.IsOpen))
        {
            if (vm.IsOpen)
            {
                _previousFocus = FocusManager.GetFocusedElement(XamlRoot) as UIElement;
                // The overlay becomes visible through its binding first: focus once layout has caught up.
                DispatcherQueue.TryEnqueue(() =>
                {
                    QueryBox.Focus(FocusState.Programmatic);
                    QueryBox.SelectAll();
                });
            }
            else
            {
                _previousFocus?.Focus(FocusState.Programmatic);
                _previousFocus = null;
            }
        }
        else if (e.PropertyName == nameof(CommandPaletteViewModel.SelectedIndex)
                 && vm.SelectedIndex >= 0 && vm.SelectedIndex < vm.Results.Count)
        {
            ResultList.ScrollIntoView(vm.Results[vm.SelectedIndex]);
        }
    }

    // The card sits at 15 % of the window height.
    private void OnOverlaySizeChanged(object sender, SizeChangedEventArgs e) =>
        Card.Margin = new Thickness(0, e.NewSize.Height * 0.15, 0, 0);

    private void OnScrimTapped(object sender, TappedRoutedEventArgs e) => ViewModel?.CloseCommand.Execute(null);

    private void OnResultClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PaletteResult result)
        {
            ViewModel?.ExecuteCommand.Execute(result);
        }
    }

    private void OnCardKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (ViewModel is not { } vm)
        {
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.Down:
                vm.MoveNextCommand.Execute(null);
                break;
            case VirtualKey.Up:
                vm.MovePreviousCommand.Execute(null);
                break;
            case VirtualKey.Enter:
                vm.ExecuteSelectedCommand.Execute(null);
                break;
            case VirtualKey.Escape:
                vm.CloseCommand.Execute(null);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    // Matched title characters are drawn in the accent colour; x:Bind cannot build the Runs.
    private void OnContainerContentChanging(ListViewBase sender, ContainerContentChangingEventArgs args)
    {
        if (args.InRecycleQueue || args.Item is not PaletteResult result
            || args.ItemContainer.ContentTemplateRoot is not Grid { Children: [StackPanel { Children: [TextBlock title, ..] }, ..] })
        {
            return;
        }

        title.Inlines.Clear();
        var text = result.Item.Title;
        for (var i = 0; i < text.Length; i++)
        {
            var run = new Run { Text = text[i].ToString() };
            if (result.TitleMatches.Contains(i))
            {
                run.Foreground = AccentProbe.Foreground;
                run.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
            }

            title.Inlines.Add(run);
        }
    }
}
