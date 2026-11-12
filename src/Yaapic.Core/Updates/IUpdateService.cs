using Yaapic.Localization;

namespace Yaapic.Core.Updates;

/// <summary>An update that can be downloaded.</summary>
/// <param name="Version">The version it installs.</param>
/// <param name="Notes">The release notes (Markdown), possibly empty.</param>
public sealed record UpdateInfo(AppVersion Version, string Notes = "");

/// <summary>The outcome of a check for updates.</summary>
public abstract record UpdateCheckResult
{
    /// <summary>The running version is the newest of the channel.</summary>
    public sealed record UpToDate : UpdateCheckResult;

    /// <summary>A newer version exists.</summary>
    /// <param name="Update">The update.</param>
    public sealed record Available(UpdateInfo Update) : UpdateCheckResult;

    /// <summary>The check could not complete (offline, feed unreachable, malformed feed).</summary>
    /// <param name="Reason">A short English reason for the console log.</param>
    public sealed record Failed(string Reason) : UpdateCheckResult;
}

/// <summary>Checks for, downloads and applies application updates.</summary>
public interface IUpdateService
{
    /// <summary>Gets a value indicating whether updates can work: false for a development run or a copy that was not installed.</summary>
    bool IsSupported { get; }

    /// <summary>Checks the feed of a channel.</summary>
    /// <param name="channel">The channel to check.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The outcome; failures are reported as <see cref="UpdateCheckResult.Failed"/>, never thrown.</returns>
    Task<UpdateCheckResult> CheckAsync(AppChannel channel, CancellationToken ct);

    /// <summary>Downloads an update found by <see cref="CheckAsync"/>.</summary>
    /// <param name="update">The update to download.</param>
    /// <param name="progress">Receives the percentage, 0 to 100.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A task completing when the update is ready to apply.</returns>
    /// <exception cref="InvalidOperationException">The service is not supported or the update is unknown.</exception>
    /// <exception cref="IOException">The download failed.</exception>
    Task DownloadAsync(UpdateInfo update, IProgress<int> progress, CancellationToken ct);

    /// <summary>Applies the downloaded update the next time the application exits.</summary>
    /// <exception cref="InvalidOperationException">No update was downloaded.</exception>
    void ApplyOnRestart();

    /// <summary>Exits the application, applies the downloaded update and starts the new version.</summary>
    /// <exception cref="InvalidOperationException">No update was downloaded.</exception>
    void RestartAndApply();
}

/// <summary>The service of a build that cannot update itself: never supported, never checks.</summary>
public sealed class UnsupportedUpdateService : IUpdateService
{
    /// <inheritdoc />
    public bool IsSupported => false;

    /// <inheritdoc />
    public Task<UpdateCheckResult> CheckAsync(AppChannel channel, CancellationToken ct) =>
        Task.FromResult<UpdateCheckResult>(new UpdateCheckResult.Failed(Strings.UpdateUnavailable));

    /// <inheritdoc />
    public Task DownloadAsync(UpdateInfo update, IProgress<int> progress, CancellationToken ct) =>
        throw new InvalidOperationException("Updates are not available in this build.");

    /// <inheritdoc />
    public void ApplyOnRestart() => throw new InvalidOperationException("Updates are not available in this build.");

    /// <inheritdoc />
    public void RestartAndApply() => throw new InvalidOperationException("Updates are not available in this build.");
}
