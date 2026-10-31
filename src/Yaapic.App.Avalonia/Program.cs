using Avalonia;

namespace Yaapic.App.Avalonia;

/// <summary>Entry point of the Avalonia front-end.</summary>
public static class Program
{
    /// <summary>Starts the desktop application.</summary>
    /// <param name="args">Command-line arguments (unused).</param>
    /// <returns>The process exit code.</returns>
    [STAThread]
    public static int Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>Builds the Avalonia application, also used by the previewer.</summary>
    /// <returns>The configured application builder.</returns>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>().UsePlatformDetect().LogToTrace();
}
