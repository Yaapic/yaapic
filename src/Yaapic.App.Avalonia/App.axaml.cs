using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Yaapic.App.Avalonia.Services;
using Yaapic.Composition;
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
            var window = new MainWindow();
            var shell = AppComposition.CreateShell(Workspace.DefaultRoot(), new AvaloniaDialogService(() => window));
            window.DataContext = shell;
            desktop.MainWindow = window;
            _ = shell.InitializeAsync();
        }
        base.OnFrameworkInitializationCompleted();
    }
}

