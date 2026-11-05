using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Yaapic.ViewModels.Shell;

namespace Yaapic.App.Avalonia.Services;

/// <summary>Drives the app through <see cref="CaptureOptions"/> and renders the focused window to a PNG (view-only wiring).</summary>
internal static class WindowCapture
{
    /// <summary>Applies the actions requested on the command line, then captures the window and exits when asked to.</summary>
    /// <param name="desktop">The desktop lifetime.</param>
    /// <param name="window">The main window.</param>
    /// <param name="shell">The initialised shell.</param>
    /// <param name="options">The parsed options.</param>
    /// <returns>A task that completes when the actions are done (the capture itself runs and shuts the app down).</returns>
    public static async Task RunAsync(IClassicDesktopStyleApplicationLifetime desktop, MainWindow window, ShellViewModel shell, CaptureOptions options)
    {
        try
        {
            await shell.InitializeAsync();
            if (options.Send && shell.Tabs.ActiveTab is { } tab)
            {
                var sent = tab.SendCommand.ExecuteAsync(null);
                await Task.WhenAny(sent, Task.Delay(options.WaitMs)); // a slow request stays in the Sending state for the capture
            }

            await Task.Delay(500); // let the tab content materialise before walking the visual tree
            if (options.Select is { } header)
            {
                window.SelectSection(header);
            }

            if (options.Open is { } name)
            {
                window.OpenNamed(name);
            }

            if (options.Screenshot is { } path)
            {
                await Task.Delay(options.WaitMs);
                Capture(desktop, path);
                desktop.Shutdown(0);
            }
        }
        catch (Exception ex) when (options.Screenshot is not null)
        {
            await Console.Error.WriteLineAsync(ex.Message);
            desktop.Shutdown(1);
        }
    }

    private static void Capture(IClassicDesktopStyleApplicationLifetime desktop, string path)
    {
        var target = desktop.Windows.LastOrDefault(w => w.IsVisible) ?? throw new InvalidOperationException("No window to capture.");
        var size = new PixelSize((int)target.Bounds.Width, (int)target.Bounds.Height);
        using var bitmap = new RenderTargetBitmap(size, new Vector(96, 96));
        bitmap.Render(target);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        bitmap.Save(path, new PngBitmapEncoderOptions());
    }
}
