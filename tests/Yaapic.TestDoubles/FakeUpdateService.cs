using Yaapic.Core.Updates;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="IUpdateService"/> that returns what the test sets and records the calls.</summary>
public sealed class FakeUpdateService : IUpdateService
{
    /// <summary>Gets or sets the support flag.</summary>
    public bool IsSupported { get; set; } = true;

    /// <summary>Gets or sets what the next check returns.</summary>
    public UpdateCheckResult Result { get; set; } = new UpdateCheckResult.UpToDate();

    /// <summary>Gets or sets a gate the download waits for; <see langword="null"/> completes at once.</summary>
    public Task? DownloadGate { get; set; }

    /// <summary>Gets or sets an error thrown by the download.</summary>
    public Exception? DownloadError { get; set; }

    /// <summary>Gets the channels checked, in order.</summary>
    public List<AppChannel> Checks { get; } = [];

    /// <summary>Gets the number of finished downloads.</summary>
    public int Downloads { get; private set; }

    /// <summary>Gets a value indicating whether <see cref="ApplyOnRestart"/> was called.</summary>
    public bool AppliedOnRestart { get; private set; }

    /// <summary>Gets a value indicating whether <see cref="RestartAndApply"/> was called.</summary>
    public bool Restarted { get; private set; }

    /// <inheritdoc/>
    public Task<UpdateCheckResult> CheckAsync(AppChannel channel, CancellationToken ct)
    {
        Checks.Add(channel);
        return Task.FromResult(Result);
    }

    /// <inheritdoc/>
    public async Task DownloadAsync(UpdateInfo update, IProgress<int> progress, CancellationToken ct)
    {
        progress.Report(0);
        if (DownloadGate is { } gate)
        {
            await gate.WaitAsync(ct);
        }

        if (DownloadError is { } error)
        {
            throw error;
        }

        progress.Report(100);
        Downloads++;
    }

    /// <inheritdoc/>
    public void ApplyOnRestart() => AppliedOnRestart = true;

    /// <inheritdoc/>
    public void RestartAndApply() => Restarted = true;
}
