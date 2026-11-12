using Yaapic.Core.Updates;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="IUpdateSettingsStore"/> kept in memory.</summary>
public sealed class InMemoryUpdateSettingsStore : IUpdateSettingsStore
{
    /// <summary>Gets or sets the stored settings.</summary>
    public UpdateSettings Settings { get; set; } = UpdateSettings.Default;

    /// <inheritdoc/>
    public Task<UpdateSettings> LoadAsync(CancellationToken ct) => Task.FromResult(Settings);

    /// <inheritdoc/>
    public Task SaveAsync(UpdateSettings settings, CancellationToken ct)
    {
        Settings = settings;
        return Task.CompletedTask;
    }
}
