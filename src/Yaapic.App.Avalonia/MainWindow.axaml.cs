using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Shell;

namespace Yaapic.App.Avalonia;

/// <summary>The themed main window, bound to the <see cref="ShellViewModel"/>.</summary>
public sealed partial class MainWindow : Window
{
    private bool _closeConfirmed;
    private bool _closePending;

    /// <summary>Creates the window and registers the keyboard shortcuts.</summary>
    public MainWindow()
    {
        InitializeComponent();
        var modifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.T, modifier), Command = new Relay(() => Shell?.Tabs.NewTab()) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.W, modifier), Command = new Relay(() => Shell?.Tabs.CloseActiveCommand.Execute(null)) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.S, modifier), Command = new Relay(() => Shell?.Tabs.SaveActiveCommand.Execute(null)) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.K, modifier), Command = new Relay(() => FilterBox.Focus()) });
    }

    private ShellViewModel? Shell => DataContext as ShellViewModel;

    /// <inheritdoc />
    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_closeConfirmed || Shell is not { } shell)
        {
            return;
        }

        e.Cancel = true;
        if (_closePending)
        {
            return;
        }

        _closePending = true;
        try
        {
            if (await shell.RequestCloseAsync())
            {
                _closeConfirmed = true;
                Close();
            }
        }
        finally
        {
            _closePending = false;
        }
    }

    private void OnAddTab(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => Shell?.Tabs.NewTab();

    private void OnCloseTab(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (Shell is { } shell && (sender as Control)?.DataContext is RequestEditorViewModel tab)
        {
            _ = shell.Tabs.CloseAsync(tab);
        }
    }

    private void OnRequestTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is RequestListItem item)
        {
            Shell?.Requests.OpenCommand.Execute(item);
        }
    }

    private void OnDeleteRequest(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is RequestListItem item)
        {
            Shell?.Requests.DeleteCommand.Execute(item);
        }
    }

    private void OnThemeDark(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => SetTheme(ThemeVariant.Dark);

    private void OnThemeLight(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => SetTheme(ThemeVariant.Light);

    private void OnThemeSystem(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => SetTheme(ThemeVariant.Default);

    private static void SetTheme(ThemeVariant variant) => Application.Current!.RequestedThemeVariant = variant;

    private sealed class Relay(Action action) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => action();
    }
}
