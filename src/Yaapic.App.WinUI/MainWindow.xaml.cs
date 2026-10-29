using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Yaapic.App.WinUI;

public sealed partial class MainWindow : Window
{
    private const int MinWidth = 900;
    private const int MinHeight = 600;

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
    }

    private void OnThemeDark(object sender, RoutedEventArgs e) => SetTheme(ElementTheme.Dark);

    private void OnThemeLight(object sender, RoutedEventArgs e) => SetTheme(ElementTheme.Light);

    // High contrast follows the OS setting: ElementTheme cannot force it.
    private void OnThemeSystem(object sender, RoutedEventArgs e) => SetTheme(ElementTheme.Default);

    private void SetTheme(ElementTheme theme) => Root.RequestedTheme = theme;
}
