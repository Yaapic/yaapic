using Yaapic.Core.Updates;

namespace Yaapic.Composition;

/// <summary>
/// A scripted update service for the screenshot tooling (<c>--update-demo &lt;mode&gt;</c>): no network, nothing written to disk.
/// Modes: <c>available</c>, <c>downloading</c> (stops at 42 %), <c>ready</c>, <c>uptodate</c> and <c>failed</c>.
/// </summary>
/// <param name="mode">The scripted behaviour.</param>
public sealed class UpdateDemoService(string mode) : IUpdateService, IUpdateSettingsStore
{
    private UpdateSettings _settings = UpdateSettings.Default;

    /// <inheritdoc />
    public bool IsSupported => true;

    /// <inheritdoc />
    public Task<UpdateCheckResult> CheckAsync(AppChannel channel, CancellationToken ct) =>
        Task.FromResult<UpdateCheckResult>(mode switch
        {
            "uptodate" => new UpdateCheckResult.UpToDate(),
            "failed" => new UpdateCheckResult.Failed("Could not reach github.com."),
            _ => new UpdateCheckResult.Available(new UpdateInfo(AppVersion.Parse("1.4.0"), "Faster startup and a new updates dialog.")),
        });

    /// <inheritdoc />
    public async Task DownloadAsync(UpdateInfo update, IProgress<int> progress, CancellationToken ct)
    {
        progress.Report(42);
        if (mode == "downloading")
        {
            await Task.Delay(Timeout.Infinite, ct);
        }

        progress.Report(100);
    }

    /// <inheritdoc />
    public void ApplyOnRestart()
    {
    }

    /// <inheritdoc />
    public void RestartAndApply()
    {
    }

    /// <inheritdoc />
    public Task<UpdateSettings> LoadAsync(CancellationToken ct) => Task.FromResult(_settings);

    /// <inheritdoc />
    public Task SaveAsync(UpdateSettings settings, CancellationToken ct)
    {
        _settings = settings;
        return Task.CompletedTask;
    }
}
