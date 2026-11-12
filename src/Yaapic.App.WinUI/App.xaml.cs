using Microsoft.UI.Xaml;
using Yaapic.App.WinUI.Services;
using Yaapic.Composition;
using Yaapic.Infrastructure.Secrets;
using Yaapic.Infrastructure.Storage;
using Yaapic.Infrastructure.Updates;
using Yaapic.ViewModels.Settings;

namespace Yaapic.App.WinUI;

/// <summary>The WinUI application; creates and activates the main window on launch.</summary>
public partial class App : Application
{
    private MainWindow? _window;

    /// <summary>Gets the language and accent settings; their accent choices are applied to the running app as they are made.</summary>
    internal SettingsViewModel Settings { get; private set; } = null!;

    /// <summary>Starts a new instance of the app with the same arguments and closes this one (the language applies at start-up).</summary>
    internal void Restart()
    {
        if (Environment.ProcessPath is { } exe)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe, Environment.GetCommandLineArgs().Skip(1).Select(a => $"\"{a}\"")) { UseShellExecute = false });
        }

        Exit();
    }

    /// <summary>Initializes the application.</summary>
    public App()
    {
        // WinUI generates Main, so this constructor is the first code of ours to run; see UpdateHooks.
        UpdateHooks.Run();
        InitializeComponent();
    }

    /// <inheritdoc/>
    protected override async void OnLaunched(LaunchActivatedEventArgs args)
    {
        var workspace = Workspace.DefaultRoot();
        string? dataDirectory = null;
        string? updateDemo = null;
        ElementTheme? requestedTheme = null;
        var argv = Environment.GetCommandLineArgs();
        for (var i = 1; i < argv.Length - 1; i++)
        {
            if (argv[i] == "--workspace")
            {
                workspace = argv[i + 1];
            }
            else if (argv[i] == "--data-dir")
            {
                dataDirectory = argv[i + 1];
            }
            else if (argv[i] == "--update-demo")
            {
                updateDemo = argv[i + 1];
            }
            else if (argv[i] == "--theme")
            {
                requestedTheme = argv[i + 1] switch
                {
                    "dark" => ElementTheme.Dark,
                    "light" => ElementTheme.Light,
                    "system" => ElementTheme.Default,
                    _ => null,
                };
            }
        }

        // The language must be applied before the window and any view exist: their texts come from Strings.
        var preferences = AppPreferences.LoadAndApplyLanguage(AppPreferences.CreateStore(dataDirectory));
        var window = new MainWindow();
        _window = window;
        if (requestedTheme is { } theme)
        {
            window.SetTheme(theme);
        }

        AccentApplier.Apply(preferences.Accent);
        Settings = new SettingsViewModel(AppPreferences.CreateStore(dataDirectory));
        Settings.AccentChanged += (_, choice) => AccentApplier.Apply(choice);
        await Settings.InitializeAsync();
        var shell = AppComposition.CreateShell(
            workspace,
            new WinUIDialogService(window, () => window.Content?.XamlRoot),
            new DpapiKeyProtectorFactory(),
            dataDirectory,
            updateDemo);
        window.Title = $"Yaapic {AppComposition.CreateAppInfo().Version}";
        window.ViewModel = shell;
        window.Activate();
        await shell.InitializeAsync();
    }
}
