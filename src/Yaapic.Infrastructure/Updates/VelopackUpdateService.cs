using Velopack;
using Velopack.Sources;
using Yaapic.Core.Updates;

namespace Yaapic.Infrastructure.Updates;

/// <summary>Runs the Velopack installer hooks; call it first in the entry point of the app.</summary>
public static class UpdateHooks
{
    /// <summary>Handles the arguments the installer, updater and uninstaller pass to the app (the process exits for those) and returns for a normal start.</summary>
    public static void Run() => VelopackApp.Build().Run();
}

/// <summary>Updates the installed app from the GitHub Releases of <c>Yaapic/yaapic</c> with Velopack.</summary>
/// <param name="managerFactory">Builds the Velopack manager of a channel (tests use a local folder and a fake install); <see langword="null"/> for GitHub Releases, whose pre-release flag follows the channel.</param>
public sealed class VelopackUpdateService(Func<AppChannel, UpdateManager>? managerFactory = null) : IUpdateService
{
    private const string Repository = "https://github.com/Yaapic/yaapic";

    private readonly Lock _gate = new();
    private UpdateManager? _manager;
    private Velopack.UpdateInfo? _found;
    private Velopack.UpdateInfo? _downloaded;

    /// <inheritdoc />
    public bool IsSupported
    {
        get
        {
            try
            {
                return (_manager ?? ManagerFor(AppChannel.Stable)).IsInstalled;
            }
            catch (InvalidOperationException)
            {
                // Velopack has no locator until UpdateHooks.Run() ran (tests, tools): nothing is installed.
                return false;
            }
        }
    }

    /// <inheritdoc />
    public async Task<UpdateCheckResult> CheckAsync(AppChannel channel, CancellationToken ct)
    {
        try
        {
            var manager = ManagerFor(channel);
            if (!manager.IsInstalled)
            {
                return new UpdateCheckResult.Failed("The app is not installed, so it cannot update itself.");
            }

            var found = await manager.CheckForUpdatesAsync().WaitAsync(ct);
            lock (_gate)
            {
                _manager = manager;
                _found = found;
                _downloaded = null;
            }

            return found is null
                ? new UpdateCheckResult.UpToDate()
                : AppVersion.TryParse(found.TargetFullRelease.Version.ToString(), out var version)
                    ? new UpdateCheckResult.Available(new Core.Updates.UpdateInfo(version, found.TargetFullRelease.NotesMarkdown ?? ""))
                    : new UpdateCheckResult.Failed($"The feed lists an unreadable version '{found.TargetFullRelease.Version}'.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Network errors, a missing release list, a malformed feed: the port reports them, it does not throw them.
            return new UpdateCheckResult.Failed(ex.Message);
        }
    }

    /// <inheritdoc />
    public async Task DownloadAsync(Core.Updates.UpdateInfo update, IProgress<int> progress, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(update);
        ArgumentNullException.ThrowIfNull(progress);
        UpdateManager manager;
        Velopack.UpdateInfo found;
        lock (_gate)
        {
            manager = _manager ?? throw new InvalidOperationException("Check for updates before downloading.");
            found = _found ?? throw new InvalidOperationException("No update was found.");
        }

        try
        {
            await manager.DownloadUpdatesAsync(found, progress.Report, cancelToken: ct);
        }
        catch (Exception ex) when (ex is not (OperationCanceledException or InvalidOperationException or OutOfMemoryException))
        {
            throw new IOException($"Cannot download the update: {ex.Message}", ex);
        }

        lock (_gate)
        {
            _downloaded = found;
        }
    }

    /// <inheritdoc />
    public void ApplyOnRestart()
    {
        var (manager, downloaded) = Downloaded();
        manager.WaitExitThenApplyUpdates(downloaded, silent: true, restart: false);
    }

    /// <inheritdoc />
    public void RestartAndApply()
    {
        var (manager, downloaded) = Downloaded();
        manager.ApplyUpdatesAndRestart(downloaded);
    }

    private (UpdateManager Manager, Velopack.UpdateInfo Update) Downloaded()
    {
        lock (_gate)
        {
            return (_manager, _downloaded) is (not null, not null)
                ? (_manager, _downloaded)
                : throw new InvalidOperationException("No update was downloaded.");
        }
    }

    private UpdateManager ManagerFor(AppChannel channel) =>
        managerFactory?.Invoke(channel) ?? new UpdateManager(new GithubSource(Repository, null, channel == AppChannel.Preview));
}
