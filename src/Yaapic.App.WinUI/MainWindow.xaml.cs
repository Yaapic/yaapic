using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Shell;

namespace Yaapic.App.WinUI;

/// <summary>The themed main window, bound to the <see cref="ShellViewModel"/>.</summary>
public sealed partial class MainWindow : Window
{
    private const int MinWidth = 900;
    private const int MinHeight = 600;

    private ShellViewModel? _viewModel;
    private bool _closeConfirmed;

    /// <summary>Initializes the window.</summary>
    public MainWindow()
    {
        InitializeComponent();
        // Mica is applied to the title bar only: the content grid paints an opaque surface.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(TitleBar);
        AppWindow.Resize(new Windows.Graphics.SizeInt32(MinWidth, MinHeight));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = MinWidth;
            presenter.PreferredMinimumHeight = MinHeight;
        }

        AppWindow.Closing += OnClosing;
    }

    /// <summary>Gets or sets the shell the window displays; setting it refreshes every binding.</summary>
    public ShellViewModel? ViewModel
    {
        get => _viewModel;
        set
        {
            _viewModel = value;
            Bindings.Update();
        }
    }

    private async void OnClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (_closeConfirmed || _viewModel is null)
        {
            return;
        }

        args.Cancel = true;
        if (await _viewModel.RequestCloseAsync())
        {
            _closeConfirmed = true;
            Close();
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

    private void OnAddTab(TabView sender, object args) => _viewModel?.Tabs.NewTab();

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

    private void OnThemeDark(object sender, RoutedEventArgs e) => SetTheme(ElementTheme.Dark);

    private void OnThemeLight(object sender, RoutedEventArgs e) => SetTheme(ElementTheme.Light);

    // High contrast follows the OS setting: ElementTheme cannot force it.
    private void OnThemeSystem(object sender, RoutedEventArgs e) => SetTheme(ElementTheme.Default);

    private void SetTheme(ElementTheme theme) => Root.RequestedTheme = theme;
}
