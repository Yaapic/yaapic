using System.Text.Json;
using System.Text.Json.Serialization;
using Yaapic.Core.Storage;
using Yaapic.Core.Updates;
using Yaapic.Infrastructure.Secrets;
using Yaapic.Infrastructure.Storage;

namespace Yaapic.Infrastructure.Updates;

/// <summary>Stores the update preferences in <c>&lt;user data dir&gt;/Yaapic/updates.json</c>: per user, outside every workspace.</summary>
/// <param name="userDataDirectory">Replaces the per-user data directory (tests, screenshot captures); <see langword="null"/> for the real one.</param>
public sealed class FileUpdateSettingsStore(string? userDataDirectory = null) : IUpdateSettingsStore
{
    private readonly string _path = Path.Combine(userDataDirectory ?? DefaultVaultLocation.UserDataDirectory(), "Yaapic", "updates.json");

    /// <inheritdoc />
    public async Task<UpdateSettings> LoadAsync(CancellationToken ct)
    {
        try
        {
            if (!File.Exists(_path))
            {
                return UpdateSettings.Default;
            }

            var dto = JsonSerializer.Deserialize(await File.ReadAllTextAsync(_path, ct), UpdateSettingsJsonContext.Default.UpdateSettingsDto);
            return dto is null
                ? UpdateSettings.Default
                : new UpdateSettings(
                    Enum.TryParse<AppChannel>(dto.Channel, ignoreCase: true, out var channel) && Enum.IsDefined(channel) ? channel : null,
                    dto.CheckAutomatically ?? true,
                    DateTimeOffset.TryParse(dto.LastCheckUtc, null, System.Globalization.DateTimeStyles.RoundtripKind, out var last) ? last : null);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return UpdateSettings.Default;
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(UpdateSettings settings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(settings);
        var dto = new UpdateSettingsDto
        {
            Channel = settings.Channel?.ToString().ToLowerInvariant(),
            CheckAutomatically = settings.CheckAutomatically,
            LastCheckUtc = settings.LastCheckUtc?.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        };
        try
        {
            await WorkspaceFiles.WriteAtomicAsync(_path, JsonSerializer.Serialize(dto, UpdateSettingsJsonContext.Default.UpdateSettingsDto) + "\n", ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StorageException($"Cannot write update settings: {ex.Message}", ex);
        }
    }
}

internal sealed class UpdateSettingsDto
{
    public string? Channel { get; set; }

    public bool? CheckAutomatically { get; set; }

    public string? LastCheckUtc { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(UpdateSettingsDto))]
internal partial class UpdateSettingsJsonContext : JsonSerializerContext;
