using Microsoft.UI.Xaml;
using Yaapic.App.WinUI.Services;
using Yaapic.Composition;
using Yaapic.Infrastructure.Storage;

namespace Yaapic.App.WinUI;

/// <summary>The WinUI application; creates and activates the main window on launch.</summary>
public partial class App : Application
{
    private MainWindow? _window;

    /// <summary>Initializes the application.</summary>
    public App() => InitializeComponent();

    /// <inheritdoc/>
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var window = new MainWindow();
        _window = window;
        var shell = AppComposition.CreateShell(
            Workspace.DefaultRoot(),
            new WinUIDialogService(() => window.Content?.XamlRoot));
        window.ViewModel = shell;
        window.Activate();
        await shell.InitializeAsync();
    }
}
