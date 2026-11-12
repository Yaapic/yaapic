using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Yaapic.App.Avalonia.Services;
using Yaapic.Composition;
using Yaapic.Core.Secrets;
using Yaapic.Infrastructure.Secrets;
using Yaapic.Infrastructure.Storage;
using Yaapic.ViewModels.Settings;

namespace Yaapic.App.Avalonia;

/// <summary>Avalonia application: loads the shared design resources and opens the main window.</summary>
public sealed class App : Application
{
    /// <summary>Gets the language and accent settings; their accent choices are applied to the running app as they are made.</summary>
    internal SettingsViewModel Settings { get; private set; } = null!;

    /// <summary>Starts a new instance of the app with the same arguments and closes this one (the language applies at start-up).</summary>
    internal void Restart()
    {
        if (Environment.ProcessPath is { } exe)
        {
            var info = new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = false };
            foreach (var arg in Environment.GetCommandLineArgs().Skip(1))
            {
                info.ArgumentList.Add(arg);
            }

            System.Diagnostics.Process.Start(info);
        }

        (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Shutdown();
    }

    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var options = CaptureOptions.Parse(desktop.Args ?? []);
            var preferences = AppPreferences.LoadAndApplyLanguage(AppPreferences.CreateStore(options.DataDirectory));
            AccentApplier.Apply(this, preferences.Accent);
            Settings = new SettingsViewModel(AppPreferences.CreateStore(options.DataDirectory));
            Settings.AccentChanged += (_, choice) => AccentApplier.Apply(this, choice);
            _ = Settings.InitializeAsync();
            var window = new MainWindow();
            var shell = AppComposition.CreateShell(options.Workspace ?? Workspace.DefaultRoot(), new AvaloniaDialogService(() => window), PlatformProtectors(), options.DataDirectory, options.UpdateDemo);
            window.Title = $"Yaapic {AppComposition.CreateAppInfo().Version}";
            window.DataContext = shell;
            if (options.Theme is { } theme)
            {
                MainWindow.ApplyTheme(theme);
            }

            if (options.Size is { } size)
            {
                window.Width = size.Width;
                window.Height = size.Height;
            }

            desktop.MainWindow = window;
            _ = WindowCapture.RunAsync(desktop, window, shell, options);
        }
        base.OnFrameworkInitializationCompleted();
    }

    // DPAPI on Windows, Keychain / Secret Service (then passphrase) on macOS and Linux.
    private static IKeyProtectorFactory PlatformProtectors() =>
        OperatingSystem.IsWindows() ? new DpapiKeyProtectorFactory() : new UnixKeyProtectorFactory();
}

