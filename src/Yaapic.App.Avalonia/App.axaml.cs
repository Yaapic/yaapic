using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Yaapic.App.Avalonia.Services;
using Yaapic.Composition;
using Yaapic.Core.Secrets;
using Yaapic.Infrastructure.Secrets;
using Yaapic.Infrastructure.Storage;

namespace Yaapic.App.Avalonia;

/// <summary>Avalonia application: loads the shared design resources and opens the main window.</summary>
public sealed class App : Application
{
    /// <inheritdoc />
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    /// <inheritdoc />
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var options = CaptureOptions.Parse(desktop.Args ?? []);
            var window = new MainWindow();
            var shell = AppComposition.CreateShell(options.Workspace ?? Workspace.DefaultRoot(), new AvaloniaDialogService(() => window), PlatformProtectors());
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

    // The Unix factory covers macOS and Linux; Windows keeps the passphrase protector until the Avalonia app offers DPAPI.
    private static IKeyProtectorFactory? PlatformProtectors() =>
        OperatingSystem.IsWindows() ? null : new UnixKeyProtectorFactory();
}

