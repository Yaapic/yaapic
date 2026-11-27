using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Yaapic.App.WinUI.Presentation;
using Yaapic.App.WinUI.Services;

namespace Yaapic.App.WinUI.Views;

/// <summary>A non-modal secondary window that hosts one view and copies the look of the main window.</summary>
public sealed partial class ToolWindow : Window
{
    private const int DefaultWidth = 1100;
    private const int DefaultHeight = 720;
    private const int MinWidth = 800;
    private const int MinHeight = 560;
    private static readonly List<ToolWindow> Open = [];

    private readonly FrameworkElement _owner;
    private bool _closed;
    private readonly Grid _root = new();
    private readonly Grid _titleBar = new() { Height = 48, BorderThickness = new Thickness(0, 0, 0, 1) };

    /// <summary>Creates the window; use <see cref="ShowOrActivate"/> to open it.</summary>
    /// <param name="title">The window title, also shown in the title bar row.</param>
    /// <param name="content">The hosted view.</param>
    /// <param name="owner">The main window whose theme and position the window follows.</param>
    public ToolWindow(string title, FrameworkElement content, MainWindow owner)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner.ThemeRoot;
        Title = title;
        SystemBackdrop = new MicaBackdrop();
        _root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        _titleBar.Margin = new Thickness(0);
        _titleBar.Children.Add(new TextBlock
        {
            Text = title,
            Margin = new Thickness(16, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Style = (Style)Application.Current.Resources["YaapicBodyStrongTextStyle"],
        });
        Grid.SetRow(content, 1);
        _root.Children.Add(_titleBar);
        _root.Children.Add(content);
        Content = _root;
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(_titleBar);
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        AppWindow.Resize(new Windows.Graphics.SizeInt32(DefaultWidth, DefaultHeight));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = MinWidth;
            presenter.PreferredMinimumHeight = MinHeight;
        }

        Center(owner.AppWindow);
        ApplyTheme();
        _owner.ActualThemeChanged += OnOwnerThemeChanged;
        Activated += OnActivated;
        Closed += OnClosed;
        Open.Add(this);
    }

    /// <summary>Gets whether the window was closed.</summary>
    internal bool IsClosed => _closed;

    /// <summary>Opens the window of a slot, or activates it when it is already open.</summary>
    /// <param name="slot">Holds the open window; a closed window is replaced on the next call.</param>
    /// <param name="title">The window title.</param>
    /// <param name="content">Creates the hosted view; called only when the window is created.</param>
    /// <param name="owner">The main window.</param>
    public static void ShowOrActivate(ref ToolWindow? slot, string title, Func<FrameworkElement> content, MainWindow owner)
    {
        ArgumentNullException.ThrowIfNull(content);
        // A ref parameter cannot be captured by the Closed handler, so a closed window is dropped from the slot here.
        if (slot is { IsClosed: false } existing)
        {
            existing.Activate();
            return;
        }

        slot = new ToolWindow(title, content(), owner);
        slot.Activate();
    }

    /// <summary>Closes every open tool window.</summary>
    internal static void CloseAll()
    {
        foreach (var window in Open.ToArray())
        {
            window.Close();
        }
    }

    private void OnOwnerThemeChanged(FrameworkElement sender, object args) => ApplyTheme();

    private void ApplyTheme()
    {
        var light = _owner.ActualTheme == ElementTheme.Light;
        _root.RequestedTheme = _owner.RequestedTheme;
        _root.Background = BrushMap.Themed("YaapicSurfaceAppBrush", light);
        _titleBar.Background = BrushMap.Themed("YaapicSurfaceAppBrush", light);
        _titleBar.BorderBrush = BrushMap.Themed("YaapicBorderSubtleBrush", light);
        CaptionColors.Apply(AppWindow.TitleBar, light);
    }

    private void Center(AppWindow ownerWindow)
    {
        var x = ownerWindow.Position.X + ((ownerWindow.Size.Width - DefaultWidth) / 2);
        var y = ownerWindow.Position.Y + ((ownerWindow.Size.Height - DefaultHeight) / 2);
        AppWindow.Move(new Windows.Graphics.PointInt32(Math.Max(0, x), Math.Max(0, y)));
    }

    private void OnActivated(object sender, WindowActivatedEventArgs args)
    {
        if (args.WindowActivationState != WindowActivationState.Deactivated)
        {
            WinUIDialogService.ActiveWindow = this;
        }
        else if (ReferenceEquals(WinUIDialogService.ActiveWindow, this))
        {
            WinUIDialogService.ActiveWindow = null;
        }
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        if (ReferenceEquals(WinUIDialogService.ActiveWindow, this))
        {
            WinUIDialogService.ActiveWindow = null;
        }

        _closed = true;
        _owner.ActualThemeChanged -= OnOwnerThemeChanged;
        Open.Remove(this);
    }
}
