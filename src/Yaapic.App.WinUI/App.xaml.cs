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
        var workspace = Workspace.DefaultRoot();
        var argv = Environment.GetCommandLineArgs();
        for (var i = 1; i < argv.Length - 1; i++)
        {
            if (argv[i] == "--workspace")
            {
                workspace = argv[i + 1];
            }
            else if (argv[i] == "--theme")
            {
                ElementTheme? theme = argv[i + 1] switch
                {
                    "dark" => ElementTheme.Dark,
                    "light" => ElementTheme.Light,
                    "system" => ElementTheme.Default,
                    _ => null,
                };
                if (theme is { } value)
                {
                    window.SetTheme(value);
                }
            }
        }

        var shell = AppComposition.CreateShell(
            workspace,
            new WinUIDialogService(() => window.Content?.XamlRoot));
        window.ViewModel = shell;
        window.Activate();
        await shell.InitializeAsync();
    }
}
