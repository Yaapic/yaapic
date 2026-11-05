using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Yaapic.ViewModels.Palette;

namespace Yaapic.App.Avalonia.Views;

/// <summary>The Ctrl+K command palette overlay, bound to a <see cref="CommandPaletteViewModel"/>.</summary>
public sealed partial class CommandPaletteView : UserControl
{
    private CommandPaletteViewModel? _observed;
    private IInputElement? _previousFocus;

    /// <summary>Creates the view.</summary>
    public CommandPaletteView()
    {
        InitializeComponent();
        // Tunnelling lets the palette see Up/Down/Enter/Esc before the focused TextBox or list does.
        Card.AddHandler(KeyDownEvent, OnCardKeyDown, RoutingStrategies.Tunnel);
    }

    /// <inheritdoc />
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (_observed is not null)
        {
            _observed.PropertyChanged -= OnPaletteChanged;
        }

        _observed = DataContext as CommandPaletteViewModel;
        if (_observed is not null)
        {
            _observed.PropertyChanged += OnPaletteChanged;
        }
    }

    private void OnPaletteChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CommandPaletteViewModel.IsOpen) || _observed is not { } vm)
        {
            return;
        }

        if (vm.IsOpen)
        {
            _previousFocus = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement();
            // The overlay becomes visible through its binding first: focus once layout has caught up.
            Dispatcher.UIThread.Post(() =>
            {
                QueryBox.Focus();
                QueryBox.SelectAll();
            });
        }
        else
        {
            _previousFocus?.Focus();
            _previousFocus = null;
        }
    }

    private void OnScrimPressed(object? sender, PointerPressedEventArgs e) => _observed?.CloseCommand.Execute(null);

    private void OnRowTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is PaletteResult result)
        {
            _observed?.ExecuteCommand.Execute(result);
        }
    }

    private void OnCardKeyDown(object? sender, KeyEventArgs e)
    {
        if (_observed is not { } vm)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                vm.MoveNextCommand.Execute(null);
                break;
            case Key.Up:
                vm.MovePreviousCommand.Execute(null);
                break;
            case Key.Enter:
                vm.ExecuteSelectedCommand.Execute(null);
                break;
            case Key.Escape:
                vm.CloseCommand.Execute(null);
                break;
            default:
                return;
        }

        e.Handled = true;
    }

    // Matched title characters are drawn in the accent colour; a binding cannot build the Runs.
    private void OnTitleDataContextChanged(object? sender, EventArgs e)
    {
        if (sender is not TextBlock title || title.DataContext is not PaletteResult result)
        {
            return;
        }

        IBrush? accent = this.TryFindResource("YaapicAccentTextBrush", ActualThemeVariant, out var brush) ? brush as IBrush : null;
        title.Inlines ??= [];
        title.Inlines.Clear();
        var text = result.Item.Title;
        for (var i = 0; i < text.Length; i++)
        {
            var run = new Run(text[i].ToString());
            if (result.TitleMatches.Contains(i))
            {
                run.Foreground = accent;
                run.FontWeight = FontWeight.SemiBold;
            }

            title.Inlines.Add(run);
        }
    }
}
