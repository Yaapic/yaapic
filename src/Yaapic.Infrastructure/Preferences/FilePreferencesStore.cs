using System.Text.Json;
using System.Text.Json.Serialization;
using Yaapic.Core.Preferences;
using Yaapic.Core.Storage;
using Yaapic.Infrastructure.Secrets;
using Yaapic.Infrastructure.Storage;

namespace Yaapic.Infrastructure.Preferences;

/// <summary>Stores the preferences in <c>&lt;user data dir&gt;/Yaapic/preferences.json</c>: per user, outside every workspace.</summary>
/// <param name="userDataDirectory">Replaces the per-user data directory (tests, screenshot captures); <see langword="null"/> for the real one.</param>
public sealed class FilePreferencesStore(string? userDataDirectory = null) : IPreferencesStore
{
    private readonly string _path = Path.Combine(userDataDirectory ?? DefaultVaultLocation.UserDataDirectory(), "Yaapic", "preferences.json");

    /// <inheritdoc />
    public async Task<UserPreferences> LoadAsync(CancellationToken ct)
    {
        try
        {
            if (!File.Exists(_path))
            {
                return UserPreferences.Default;
            }

            var dto = JsonSerializer.Deserialize(await File.ReadAllTextAsync(_path, ct), PreferencesJsonContext.Default.PreferencesDto);
            return dto is null
                ? UserPreferences.Default
                : new UserPreferences(
                    Enum.TryParse<AppLanguage>(dto.Language, ignoreCase: true, out var language) && Enum.IsDefined(language) ? language : AppLanguage.System,
                    AccentChoice.IsValid(dto.Accent) ? dto.Accent! : AccentChoice.Default);
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return UserPreferences.Default;
        }
    }

    /// <inheritdoc />
    public async Task SaveAsync(UserPreferences preferences, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        var dto = new PreferencesDto { Language = preferences.Language.ToString().ToLowerInvariant(), Accent = preferences.Accent };
        try
        {
            await WorkspaceFiles.WriteAtomicAsync(_path, JsonSerializer.Serialize(dto, PreferencesJsonContext.Default.PreferencesDto) + "\n", ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StorageException($"Cannot write preferences: {ex.Message}", ex);
        }
    }
}

internal sealed class PreferencesDto
{
    public string? Language { get; set; }

    public string? Accent { get; set; }
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PreferencesDto))]
internal partial class PreferencesJsonContext : JsonSerializerContext;
