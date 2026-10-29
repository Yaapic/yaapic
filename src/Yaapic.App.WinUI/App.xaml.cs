using Microsoft.UI.Xaml;

namespace Yaapic.App.WinUI;

/// <summary>The WinUI application; creates and activates the main window on launch.</summary>
public partial class App : Application
{
    private Window? _window;

    /// <summary>Initializes the application.</summary>
    public App() => InitializeComponent();

    /// <inheritdoc/>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
