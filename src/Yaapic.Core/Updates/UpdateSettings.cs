namespace Yaapic.Core.Updates;

/// <summary>The per-user update preferences.</summary>
/// <param name="Channel">The channel the user chose, or <see langword="null"/> to follow the channel of the running version.</param>
/// <param name="CheckAutomatically">Whether the app checks for updates by itself.</param>
/// <param name="LastCheckUtc">When the last automatic or manual check ran, or <see langword="null"/> when none did.</param>
public sealed record UpdateSettings(AppChannel? Channel = null, bool CheckAutomatically = true, DateTimeOffset? LastCheckUtc = null)
{
    /// <summary>Gets the settings of a user who never changed anything.</summary>
    public static UpdateSettings Default { get; } = new();

    /// <summary>Gets the channel to check: the chosen one, else the channel of the running version.</summary>
    /// <param name="app">The running application.</param>
    /// <returns>The effective channel.</returns>
    public AppChannel EffectiveChannel(IAppInfo app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return Channel ?? app.Channel;
    }
}

/// <summary>Persists <see cref="UpdateSettings"/>.</summary>
public interface IUpdateSettingsStore
{
    /// <summary>Loads the settings.</summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The saved settings, or <see cref="UpdateSettings.Default"/> when they are missing or unreadable; never throws for those cases.</returns>
    Task<UpdateSettings> LoadAsync(CancellationToken ct);

    /// <summary>Saves the settings.</summary>
    /// <param name="settings">The settings to save.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <exception cref="Yaapic.Core.Storage.StorageException">The store cannot be written.</exception>
    Task SaveAsync(UpdateSettings settings, CancellationToken ct);
}

/// <summary>Decides when the app checks for updates by itself.</summary>
/// <param name="clock">The clock.</param>
public sealed class UpdateScheduler(TimeProvider clock)
{
    /// <summary>The minimum time between two automatic checks.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    /// <summary>Tells whether an automatic check is due now.</summary>
    /// <param name="settings">The user settings.</param>
    /// <returns><see langword="true"/> when automatic checks are on and none ran in the last 24 hours.</returns>
    public bool IsCheckDue(UpdateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.CheckAutomatically && (settings.LastCheckUtc is not { } last || clock.GetUtcNow() - last >= Interval);
    }

    /// <summary>Returns the settings with the last check set to now.</summary>
    /// <param name="settings">The user settings.</param>
    /// <returns>The updated settings.</returns>
    public UpdateSettings MarkChecked(UpdateSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings with { LastCheckUtc = clock.GetUtcNow() };
    }
}
