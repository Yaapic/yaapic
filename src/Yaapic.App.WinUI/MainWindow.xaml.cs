using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
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
    }

    // The caption buttons follow the OS theme, not the element theme forced by --theme or the Settings menu: colour them
    // from the tokens (text.primary / text.secondary, overlay.hover) so they stay visible on both surfaces.
    private void ApplyCaptionButtonColors()
    {
        var light = Root.ActualTheme == ElementTheme.Light;
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
                _viewModel.Tabs.PropertyChanged -= OnTabsChanged;
                _viewModel.Requests.Items.CollectionChanged -= OnItemsChanged;
            }

            _viewModel = value;
            Bindings.Update();
            if (_viewModel is not null)
            {
                _viewModel.Tabs.PropertyChanged += OnTabsChanged;
                _viewModel.Requests.Items.CollectionChanged += OnItemsChanged;
                SyncListSelection();
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

    private void OnAddTab(object sender, RoutedEventArgs e) => _viewModel?.Tabs.NewTab();

    private void OnTabCloseRequested(TabView sender, TabViewTabCloseRequestedEventArgs args)
    {
        if (_viewModel is not null && args.Item is RequestEditorViewModel tab)
        {
            _ = _viewModel.Tabs.CloseAsync(tab);
        }
    }

    private void OnRequestClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RequestListItem item)
        {
            _viewModel?.Requests.OpenCommand.Execute(item);
        }
    }

    private void OnDeleteRequest(object sender, RoutedEventArgs e)
    {
        if (((FrameworkElement)sender).Tag is RequestListItem item)
        {
            _viewModel?.Requests.DeleteCommand.Execute(item);
        }
    }

    private void OnTabsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TabsViewModel.ActiveTab))
        {
            SyncListSelection();
        }
    }

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => SyncListSelection();

    // The list selection mirrors the active tab; ItemClick (not SelectionChanged) opens requests, so this never opens one.
    private void SyncListSelection()
    {
        var id = _viewModel?.Tabs.ActiveTab?.Id;
        RequestList.SelectedItem = id is null ? null : _viewModel?.Requests.Items.FirstOrDefault(i => i.Id == id);
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
            Text = "No environment",
            GroupName = "Environment",
            IsChecked = environments.ActiveEnvironment is null,
        };
        none.Click += (_, _) => environments.ActiveEnvironment = null;
        EnvironmentMenu.Items.Add(none);
        EnvironmentMenu.Items.Add(new MenuFlyoutSeparator());
        var manage = new MenuFlyoutItem { Text = "Manage environments..." };
        manage.Click += OnManageEnvironments;
        EnvironmentMenu.Items.Add(manage);
    }

    private void OnEnvironmentsShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        OnManageEnvironments(sender, new RoutedEventArgs());
        args.Handled = true;
    }

    private void OnFilterShortcut(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
    {
        FilterBox.Focus(FocusState.Keyboard);
        args.Handled = true;
    }

    private async void OnManageEnvironments(object sender, RoutedEventArgs e)
    {
        // One ContentDialog at a time: ignore the shortcut / rail click while a dialog is already open.
        if (_viewModel is not null && !_dialogOpen)
        {
            _dialogOpen = true;
            try
            {
                await new Views.EnvironmentsDialog(_viewModel.Environments) { XamlRoot = Root.XamlRoot }.ShowAsync();
            }
            finally
            {
                _dialogOpen = false;
            }
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
