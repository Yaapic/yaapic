using Yaapic.Localization;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Yaapic.ViewModels.Collections;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Shell;
using Yaapic.ViewModels.Tabs;

namespace Yaapic.App.WinUI;

/// <summary>The themed main window, bound to the <see cref="ShellViewModel"/>.</summary>
public sealed partial class MainWindow : Window
{
    private const int MinWidth = 900;
    private const int MinHeight = 600;

    private ShellViewModel? _viewModel;
    private bool _closeConfirmed;
    private bool _closePending;
    private bool _dialogOpen;
    private const double ConsoleReservedHeight = 300;
    private const VirtualKey ConsoleToggleKey = (VirtualKey)0xC0;

    /// <summary>Initializes the window.</summary>
    public MainWindow()
    {
        InitializeComponent();
        // Mica is applied to the title bar only: the content grid paints an opaque surface.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        // Tall caption buttons (48 px) match the 48 px title bar; the standard 32 px ones look undersized.
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(MinWidth, MinHeight));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = MinWidth;
            presenter.PreferredMinimumHeight = MinHeight;
        }

        AppWindow.Closing += OnClosing;
        Root.ActualThemeChanged += (_, _) => ApplyCaptionButtonColors();
        ApplyCaptionButtonColors();
        // Ctrl+` (the OEM 3 key, 0xC0 on a US layout) has no named VirtualKey and no KeyboardAccelerator match: handle the key.
        Root.KeyDown += OnRootKeyDown;
        // The console never takes the whole window: the request area keeps at least 300 dips (title bar, tabs and status bar included).
        Root.SizeChanged += (_, e) => ConsolePanel.MaxHeight = Math.Max(ShellViewModel.MinConsoleHeight, e.NewSize.Height - ConsoleReservedHeight);
    }

    // The caption buttons follow the OS theme, not the element theme forced by --theme or the Settings menu: colour them
    // from the tokens (text.primary / text.secondary, overlay.hover) so they stay visible on both surfaces.
    private void ApplyCaptionButtonColors()
    {
        var light = Root.ActualTheme == ElementTheme.Light;
        Presentation.BrushMap.IsLight = light;
        Presentation.MethodBrush.RefreshAll();
        Bindings?.Update(); // the rail brushes come from BrushMap and are bound once
        var titleBar = AppWindow.TitleBar;
        var foreground = light ? Windows.UI.Color.FromArgb(0xFF, 0x4A, 0x51, 0x5B) : Windows.UI.Color.FromArgb(0xFF, 0xA3, 0xAA, 0xB4);
        var hoverForeground = light ? Windows.UI.Color.FromArgb(0xFF, 0x16, 0x19, 0x1D) : Windows.UI.Color.FromArgb(0xFF, 0xEC, 0xEE, 0xF1);
        var hover = light ? Windows.UI.Color.FromArgb(0x0D, 0x14, 0x18, 0x1E) : Windows.UI.Color.FromArgb(0x0F, 0xFF, 0xFF, 0xFF);
        titleBar.ButtonForegroundColor = foreground;
        titleBar.ButtonInactiveForegroundColor = foreground;
        titleBar.ButtonHoverForegroundColor = hoverForeground;
        titleBar.ButtonPressedForegroundColor = hoverForeground;
        titleBar.ButtonHoverBackgroundColor = hover;
    }

    /// <summary>Gets or sets the shell the window displays; setting it refreshes every binding.</summary>
    public ShellViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            if (_viewModel is not null)
            {
                _viewModel.Collections.PropertyChanged -= OnCollectionsChanged;
                _viewModel.EnvironmentsRequested -= OnEnvironmentsRequested;
                _viewModel.Vault.SetupRequested -= OnVaultRequested;
                _viewModel.Vault.UnlockRequested -= OnVaultRequested;
                _viewModel.Updates.SettingsRequested -= OnUpdatesRequested;
            }

            _viewModel = value;
            Bindings.Update();
            if (_viewModel is not null)
            {
                _viewModel.Collections.PropertyChanged += OnCollectionsChanged;
                _viewModel.EnvironmentsRequested += OnEnvironmentsRequested;
                _viewModel.Vault.SetupRequested += OnVaultRequested;
                _viewModel.Vault.UnlockRequested += OnVaultRequested;
                _viewModel.Updates.SettingsRequested += OnUpdatesRequested;
            }
        }
    }

    private async void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_closeConfirmed || _viewModel is null)
        {
            return;
        }

        args.Cancel = true;
        // A second click on the caption close button while the save prompt is open must not open a second dialog
        // (WinUI allows one ContentDialog at a time and throws otherwise).
        if (_closePending)
        {
            return;
        }

        _closePending = true;
        try
        {
            if (await _viewModel.RequestCloseAsync())
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

    private void OnRootKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == ConsoleToggleKey
            && Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
        {
            _viewModel?.ToggleConsoleCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnNewTab(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _viewModel?.Tabs.NewTab();
        args.Handled = true;
    }

    private void OnCloseTab(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _viewModel?.Tabs.CloseActiveCommand.Execute(null);
        args.Handled = true;
    }

    private void OnSaveTab(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _viewModel?.Tabs.SaveActiveCommand.Execute(null);
        args.Handled = true;
    }

    private void OnEditorSaveRequested(object sender, RoutedEventArgs e) => _viewModel?.Tabs.SaveActiveCommand.Execute(null);

    private void OnAddTab(object sender, RoutedEventArgs e) => _viewModel?.Tabs.NewTab();

    private void OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (_viewModel is not null && args.Item is RequestEditorViewModel tab)
        {
            _ = _viewModel.Tabs.CloseAsync(tab);
        }
    }

    private void OnNodeInvoked(TreeView sender, TreeViewItemInvokedEventArgs args)
    {
        if (args.InvokedItem is CollectionNodeViewModel node)
        {
            _viewModel?.Collections.OpenCommand.Execute(node);
        }
    }

    private void OnNodeContextRequested(UIElement sender, ContextRequestedEventArgs args)
    {
        if (_viewModel is null || NodeAt(args.OriginalSource as DependencyObject) is not { } node)
        {
            return;
        }

        var tree = _viewModel.Collections;
        var menu = new MenuFlyout();
        if (node.IsFolder)
        {
            AddItem(menu, Strings.PaletteNewRequest, tree.NewRequestCommand, node);
            AddItem(menu, Strings.CollectionsNewFolder, tree.NewFolderCommand, node);
            AddItem(menu, Strings.CommonRename, tree.RenameFolderCommand, node);
            AddItem(menu, Strings.SettingsTitle, tree.EditSettingsCommand, node);
            AddItem(menu, Strings.CommonDelete, tree.DeleteFolderCommand, node);
        }
        else
        {
            AddItem(menu, Strings.CommonRename, tree.RenameRequestCommand, node);
            AddItem(menu, Strings.CommonDuplicate, tree.DuplicateRequestCommand, node);
            AddItem(menu, Strings.CommonDelete, tree.DeleteRequestCommand, node);
        }

        var targets = tree.GetMoveTargets(node);
        if (targets.Count > 0)
        {
            var moveTo = new MenuFlyoutSubItem { Text = Strings.CollectionsMoveTo };
            foreach (var target in targets)
            {
                AddItem(moveTo.Items, target.Path, tree.MoveCommand, new MoveRequest(node, target));
            }

            menu.Items.Add(moveTo);
        }

        args.Handled = true;
        if (args.TryGetPosition((UIElement)sender, out var point))
        {
            menu.ShowAt((UIElement)sender, point);
        }
        else
        {
            menu.ShowAt((FrameworkElement)args.OriginalSource);
        }
    }

    private static void AddItem(MenuFlyout menu, string text, System.Windows.Input.ICommand command, object parameter) =>
        AddItem(menu.Items, text, command, parameter);

    private static void AddItem(IList<MenuFlyoutItemBase> items, string text, System.Windows.Input.ICommand command, object parameter) =>
        items.Add(new MenuFlyoutItem { Text = text, Command = command, CommandParameter = parameter });

    private static CollectionNodeViewModel? NodeAt(DependencyObject? element)
    {
        for (; element is not null; element = VisualTreeHelper.GetParent(element))
        {
            if (element is FrameworkElement { DataContext: CollectionNodeViewModel node })
            {
                return node;
            }

            if (element is TreeViewItem { Content: CollectionNodeViewModel content })
            {
                return content;
            }
        }

        return null;
    }

    private void OnTreeKeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_viewModel is null || CollectionTree.SelectedItem is not CollectionNodeViewModel node)
        {
            return;
        }

        var tree = _viewModel.Collections;
        var command = e.Key switch
        {
            VirtualKey.F2 => node.IsFolder ? tree.RenameFolderCommand : tree.RenameRequestCommand,
            VirtualKey.Delete => node.IsFolder ? tree.DeleteFolderCommand : tree.DeleteRequestCommand,
            _ => null,
        };
        if (command is not null)
        {
            command.Execute(node);
            e.Handled = true;
        }
    }

    private void OnEnvironmentMenuOpening(object? sender, object e)
    {
        if (_viewModel is null)
        {
            return;
        }

        var environments = _viewModel.Environments;
        EnvironmentMenu.Items.Clear();
        foreach (var environment in environments.Environments)
        {
            var item = new RadioMenuFlyoutItem
            {
                Text = environment.Name,
                GroupName = "Environment",
                IsChecked = environment == environments.ActiveEnvironment,
            };
            item.Click += (_, _) => environments.ActiveEnvironment = environment;
            EnvironmentMenu.Items.Add(item);
        }

        var none = new RadioMenuFlyoutItem
        {
            Text = Strings.PaletteNoEnvironment,
            GroupName = "Environment",
            IsChecked = environments.ActiveEnvironment is null,
        };
        none.Click += (_, _) => environments.ActiveEnvironment = null;
        EnvironmentMenu.Items.Add(none);
        EnvironmentMenu.Items.Add(new MenuFlyoutSeparator());
        var manage = new MenuFlyoutItem { Text = Strings.EnvironmentsManage };
        manage.Click += OnManageEnvironments;
        EnvironmentMenu.Items.Add(manage);
    }

    private void OnEnvironmentsShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        OnManageEnvironments(sender, new RoutedEventArgs());
        args.Handled = true;
    }

    private void OnPaletteShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _viewModel?.OpenPaletteCommand.Execute(null);
        args.Handled = true;
    }

    private void OnNextTabShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _viewModel?.Tabs.NextTabCommand.Execute(null);
        args.Handled = true;
    }

    private void OnPreviousTabShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        _viewModel?.Tabs.PreviousTabCommand.Execute(null);
        args.Handled = true;
    }

    private void OnEnvironmentsRequested(object? sender, EventArgs e) => OnManageEnvironments(this, new RoutedEventArgs());

    private void OnFilterShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        FilterBox.Focus(FocusState.Keyboard);
        args.Handled = true;
    }

    private async void OnCollectionsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(CollectionTreeViewModel.SettingsEditor) || _viewModel?.Collections is not { SettingsEditor: { } editor } tree)
        {
            return;
        }

        // One ContentDialog at a time: drop the request while another dialog is open.
        if (_dialogOpen)
        {
            tree.CloseSettingsCommand.Execute(null);
            return;
        }

        _dialogOpen = true;
        try
        {
            await new Views.FolderSettingsDialog(editor) { XamlRoot = Root.XamlRoot, RequestedTheme = Root.RequestedTheme }.ShowAsync();
        }
        finally
        {
            _dialogOpen = false;
            tree.CloseSettingsCommand.Execute(null);
        }
    }

    private void OnVariableScopeOpenRequested(object? sender, Views.VariableScopeOpenRequest request)
    {
        if (_viewModel is null)
        {
            return;
        }

        if (request.Scope == Yaapic.Core.Variables.VariableScopeKind.Collection)
        {
            // The settings dialog opens through OnCollectionsChanged once the editor is set.
            _viewModel.Collections.OpenCollectionSettings(request.RequestId);
            return;
        }

        var environments = _viewModel.Environments;
        if (request.Scope == Yaapic.Core.Variables.VariableScopeKind.Globals)
        {
            environments.IsEditingGlobals = true;
        }
        else if (environments.ActiveEnvironment is { } active)
        {
            environments.EditedEnvironment = active;
        }

        OnManageEnvironments(this, new RoutedEventArgs());
    }

    private async void OnManageEnvironments(object sender, RoutedEventArgs e)
    {
        // One ContentDialog at a time: ignore the shortcut / rail click while a dialog is already open.
        if (_viewModel is not null && !_dialogOpen)
        {
            _dialogOpen = true;
            try
            {
                await new Views.EnvironmentsDialog(_viewModel.Environments, _viewModel.Globals) { XamlRoot = Root.XamlRoot, RequestedTheme = Root.RequestedTheme }.ShowAsync();
            }
            finally
            {
                _dialogOpen = false;
            }
        }
    }

    private async void OnVaultRequested(object? sender, EventArgs e)
    {
        if (_viewModel is null || _dialogOpen)
        {
            return;
        }

        _dialogOpen = true;
        try
        {
            await new Views.VaultDialog(_viewModel.Vault) { XamlRoot = Root.XamlRoot, RequestedTheme = Root.RequestedTheme }.ShowAsync();
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private void OnUpdatesClick(object sender, RoutedEventArgs e) => OnUpdatesRequested(sender, EventArgs.Empty);

    private async void OnUpdatesRequested(object? sender, EventArgs e)
    {
        if (_viewModel is null || _dialogOpen)
        {
            return;
        }

        _dialogOpen = true;
        try
        {
            await new Views.UpdatesDialog(_viewModel.Updates, ((App)Application.Current).Settings, ((App)Application.Current).Restart) { XamlRoot = Root.XamlRoot, RequestedTheme = Root.RequestedTheme }.ShowAsync();
        }
        finally
        {
            _dialogOpen = false;
        }
    }

    private void OnThemeDark(object sender, RoutedEventArgs e) => SetTheme(ElementTheme.Dark);

    private void OnThemeLight(object sender, RoutedEventArgs e) => SetTheme(ElementTheme.Light);

    // High contrast follows the OS setting: ElementTheme cannot force it.
    private void OnThemeSystem(object sender, RoutedEventArgs e) => SetTheme(ElementTheme.Default);

    /// <summary>Applies a theme, shared by the Settings menu and the <c>--theme</c> command-line option.</summary>
    /// <param name="theme">The element theme to apply.</param>
    internal void SetTheme(ElementTheme theme) => Root.RequestedTheme = theme;
}
