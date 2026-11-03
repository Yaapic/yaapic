using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Yaapic.ViewModels.Collections;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Shell;
using Yaapic.ViewModels.Tabs;

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
        // The system caption buttons overlay the right of the bar on Windows; macOS puts the traffic lights on the left.
        TitleBar.Padding = new Thickness(OperatingSystem.IsMacOS() ? 80 : 12, 0, OperatingSystem.IsWindows() ? 140 : 12, 0);
        var modifier = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.T, modifier), Command = new Relay(() => Shell?.Tabs.NewTab()) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.W, modifier), Command = new Relay(() => Shell?.Tabs.CloseActiveCommand.Execute(null)) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.S, modifier), Command = new Relay(() => Shell?.Tabs.SaveActiveCommand.Execute(null)) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.E, modifier | KeyModifiers.Shift), Command = new Relay(() => _ = ManageEnvironmentsAsync()) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.F, modifier | KeyModifiers.Shift), Command = new Relay(() => FilterBox.Focus()) });
        KeyBindings.Add(new KeyBinding { Gesture = new KeyGesture(Key.K, modifier), Command = new Relay(() => FilterBox.Focus()) });
    }

    private async void OnManageEnvironments(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => await ManageEnvironmentsAsync();

    private async Task ManageEnvironmentsAsync()
    {
        if (Shell is { } shell)
        {
            await new Views.EnvironmentsWindow { DataContext = shell }.ShowDialog(this);
        }
    }

    private void OnEnvironmentMenuOpening(object? sender, EventArgs e)
    {
        if (sender is not MenuFlyout flyout || Shell is not { } shell)
        {
            return;
        }

        var environments = shell.Environments;
        flyout.Items.Clear();
        foreach (var item in environments.Environments)
        {
            var captured = item;
            var entry = new MenuItem { Header = item.Name, ToggleType = MenuItemToggleType.Radio, GroupName = "env", IsChecked = item == environments.ActiveEnvironment };
            entry.Click += (_, _) => environments.ActiveEnvironment = captured;
            flyout.Items.Add(entry);
        }

        var none = new MenuItem { Header = "No environment", ToggleType = MenuItemToggleType.Radio, GroupName = "env", IsChecked = environments.ActiveEnvironment is null };
        none.Click += (_, _) => environments.ActiveEnvironment = null;
        flyout.Items.Add(none);
        flyout.Items.Add(new Separator());
        var manage = new MenuItem { Header = "Manage environments..." };
        manage.Click += (_, _) => _ = ManageEnvironmentsAsync();
        flyout.Items.Add(manage);
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

    private void OnSaveRequested(object? sender, EventArgs e) => Shell?.Tabs.SaveActiveCommand.Execute(null);

    private void OnNodeTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is CollectionNodeViewModel node)
        {
            Shell?.Collections.OpenCommand.Execute(node);
        }
    }

    // Selecting a request opens it, which also covers the keyboard; a folder only toggles on Tapped.
    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (e.AddedItems is [CollectionNodeViewModel { IsFolder: false } node, ..])
        {
            Shell?.Collections.OpenCommand.Execute(node);
        }
    }

    private void OnNodeContextRequested(object? sender, ContextRequestedEventArgs e)
    {
        if (Shell is not { } shell || (e.Source as Visual)?.FindAncestorOfType<TreeViewItem>() is not { DataContext: CollectionNodeViewModel node } item)
        {
            return;
        }

        var tree = shell.Collections;
        var menu = new ContextMenu();
        if (node.IsFolder)
        {
            AddItem(menu.Items, "New request", tree.NewRequestCommand, node);
            AddItem(menu.Items, "New folder", tree.NewFolderCommand, node);
            AddItem(menu.Items, "Rename", tree.RenameFolderCommand, node);
            AddItem(menu.Items, "Delete", tree.DeleteFolderCommand, node);
        }
        else
        {
            AddItem(menu.Items, "Rename", tree.RenameRequestCommand, node);
            AddItem(menu.Items, "Duplicate", tree.DuplicateRequestCommand, node);
            AddItem(menu.Items, "Delete", tree.DeleteRequestCommand, node);
        }

        var targets = tree.GetMoveTargets(node);
        if (targets.Count > 0)
        {
            var moveTo = new MenuItem { Header = "Move to" };
            foreach (var target in targets)
            {
                AddItem(moveTo.Items, target.Path, tree.MoveCommand, new MoveRequest(node, target));
            }

            menu.Items.Add(moveTo);
        }

        e.Handled = true;
        menu.Open(item);
    }

    private static void AddItem(global::Avalonia.Controls.ItemCollection items, string header, System.Windows.Input.ICommand command, object parameter) =>
        items.Add(new MenuItem { Header = header, Command = command, CommandParameter = parameter });

    private void OnTreeKeyDown(object? sender, KeyEventArgs e)
    {
        if (Shell is not { } shell || CollectionTree.SelectedItem is not CollectionNodeViewModel node)
        {
            return;
        }

        var tree = shell.Collections;
        var delete = e.Key == Key.Delete || (OperatingSystem.IsMacOS() && e.Key == Key.Back && e.KeyModifiers == KeyModifiers.Meta);
        var command = e.Key == Key.F2
            ? (node.IsFolder ? tree.RenameFolderCommand : tree.RenameRequestCommand)
            : delete ? (node.IsFolder ? tree.DeleteFolderCommand : tree.DeleteRequestCommand) : null;
        if (command is not null)
        {
            command.Execute(node);
            e.Handled = true;
        }
    }

    private void OnThemeDark(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => SetTheme(ThemeVariant.Dark);

    private void OnThemeLight(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => SetTheme(ThemeVariant.Light);

    private void OnThemeSystem(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e) => SetTheme(ThemeVariant.Default);

    private static void SetTheme(ThemeVariant variant) => Application.Current!.RequestedThemeVariant = variant;

    /// <summary>Applies a theme by name (<c>dark</c>, <c>light</c>, anything else follows the system); shared with the launch options.</summary>
    /// <param name="name">The theme name.</param>
    internal static void ApplyTheme(string name) => SetTheme(name.ToLowerInvariant() switch
    {
        "dark" => ThemeVariant.Dark,
        "light" => ThemeVariant.Light,
        _ => ThemeVariant.Default,
    });

    /// <summary>Selects the request section tab whose header starts with <paramref name="header"/>.</summary>
    /// <param name="header">The header text, for example <c>Body</c>.</param>
    /// <exception cref="InvalidOperationException">No such tab exists.</exception>
    internal void SelectSection(string header)
    {
        foreach (var tabs in this.GetVisualDescendants().OfType<TabControl>())
        {
            foreach (var item in tabs.Items.OfType<TabItem>())
            {
                var text = item.Header switch
                {
                    StackPanel { Children: [TextBlock label, ..] } => label.Text,
                    TextBlock block => block.Text,
                    _ => item.Header as string,
                };
                if (text is not null && text.StartsWith(header, StringComparison.OrdinalIgnoreCase))
                {
                    tabs.SelectedItem = item;
                    return;
                }
            }
        }

        throw new InvalidOperationException($"Section tab '{header}' not found.");
    }

    /// <summary>Opens a named window or prompt for the screenshot tooling.</summary>
    /// <param name="name"><c>environments</c>, <c>prompt</c>, <c>unsaved</c> or <c>empty</c>.</param>
    /// <exception cref="InvalidOperationException">Unknown name or nothing to act on.</exception>
    internal void OpenNamed(string name)
    {
        switch (name.ToLowerInvariant())
        {
            case "environments" when Shell is { } shell:
                shell.Environments.EditedEnvironment ??= shell.Environments.ActiveEnvironment;
                _ = ManageEnvironmentsAsync();
                break;
            case "unsaved" when Shell?.Tabs.ActiveTab is { } tab:
                tab.Name += "x";
                _ = Shell.Tabs.CloseAsync(tab);
                break;
            case "prompt":
                _ = new Views.PromptWindow("Rename request", "Request name", "Create user").ShowDialog<string?>(this);
                break;
            case "empty" when Shell?.Tabs.ActiveTab is { } open:
                _ = Shell.Tabs.CloseAsync(open);
                break;
            default:
                throw new InvalidOperationException($"Cannot open '{name}'.");
        }
    }

    private sealed class Relay(Action action) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter) => action();
    }
}
