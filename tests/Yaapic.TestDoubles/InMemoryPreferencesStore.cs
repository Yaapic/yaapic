using Yaapic.Core.Preferences;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="IPreferencesStore"/> kept in memory.</summary>
public sealed class InMemoryPreferencesStore : IPreferencesStore
{
    /// <summary>Gets or sets the stored preferences.</summary>
    public UserPreferences Preferences { get; set; } = UserPreferences.Default;

    /// <inheritdoc/>
    public Task<UserPreferences> LoadAsync(CancellationToken ct) => Task.FromResult(Preferences);

    /// <inheritdoc/>
    public Task SaveAsync(UserPreferences preferences, CancellationToken ct)
    {
        Preferences = preferences;
        return Task.CompletedTask;
    }
}
