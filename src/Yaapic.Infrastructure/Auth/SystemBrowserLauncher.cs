using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Yaapic.Core.Auth.OAuth2;
using Yaapic.Localization;

namespace Yaapic.Infrastructure.Auth;

/// <summary>Opens http and https URLs in the user's default browser on Windows, macOS and Linux.</summary>
public sealed class SystemBrowserLauncher : IExternalBrowser
{
    private readonly OSPlatform _platform;
    private readonly Action<ProcessStartInfo> _start;

    /// <summary>Creates a launcher for the current operating system that starts real processes.</summary>
    public SystemBrowserLauncher()
        : this(CurrentPlatform(), static p => Process.Start(p)?.Dispose())
    {
    }

    internal SystemBrowserLauncher(OSPlatform platform, Action<ProcessStartInfo> start)
    {
        _platform = platform;
        _start = start;
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentException">The URL is not an absolute http or https URL.</exception>
    /// <exception cref="InvalidOperationException">The browser could not be started.</exception>
    public Task OpenAsync(Uri url, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(url);
        ct.ThrowIfCancellationRequested();
        if (!url.IsAbsoluteUri || (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("Only absolute http and https URLs can be opened.", nameof(url));
        }

        var target = url.AbsoluteUri;
        ProcessStartInfo info;
        if (_platform == OSPlatform.Windows)
        {
            info = new ProcessStartInfo(target) { UseShellExecute = true };
        }
        else
        {
            info = new ProcessStartInfo(_platform == OSPlatform.OSX ? "open" : "xdg-open") { UseShellExecute = false };
            info.ArgumentList.Add(target);
        }

        try
        {
            _start(info);
        }
        catch (Exception e) when (e is Win32Exception or InvalidOperationException)
        {
            throw new InvalidOperationException(Strings.Oauth2BrowserFailed, e);
        }

        return Task.CompletedTask;
    }

    private static OSPlatform CurrentPlatform() =>
        OperatingSystem.IsWindows() ? OSPlatform.Windows : OperatingSystem.IsMacOS() ? OSPlatform.OSX : OSPlatform.Linux;
}
