using System.Globalization;

namespace Yaapic.Core.Preferences;

/// <summary>The language of the user interface.</summary>
public enum AppLanguage
{
    /// <summary>Follow the system language: French when it is French, else English.</summary>
    System,

    /// <summary>English.</summary>
    English,

    /// <summary>French.</summary>
    French,
}

/// <summary>The accent colour choices: a preset id, <see cref="System"/> or a custom <c>#RRGGBB</c> value.</summary>
public static class AccentChoice
{
    /// <summary>The default preset id.</summary>
    public const string Default = "teal";

    /// <summary>Follow the operating system accent where the platform has one.</summary>
    public const string System = "system";

    /// <summary>Gets the ids of the preset palette, in display order.</summary>
    public static IReadOnlyList<string> Presets { get; } = ["teal", "blue", "purple", "pink", "red", "orange", "green", "grey"];

    /// <summary>Tells whether a value is a preset id, <see cref="System"/> or a <c>#RRGGBB</c> colour.</summary>
    /// <param name="value">The value to check.</param>
    /// <returns><see langword="true"/> when the value is a valid choice.</returns>
    public static bool IsValid(string? value) =>
        value is not null && (value == System || Presets.Contains(value) || TryParseCustom(value, out _));

    /// <summary>Parses a custom <c>#RRGGBB</c> colour.</summary>
    /// <param name="value">The text, with the leading <c>#</c>.</param>
    /// <param name="rgb">The colour as <c>0xRRGGBB</c>.</param>
    /// <returns><see langword="true"/> when the text is exactly <c>#</c> followed by six hexadecimal digits.</returns>
    public static bool TryParseCustom(string? value, out int rgb)
    {
        rgb = 0;
        return value is { Length: 7 } && value[0] == '#' &&
            int.TryParse(value.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out rgb);
    }
}

/// <summary>The per-user appearance preferences.</summary>
/// <param name="Language">The interface language.</param>
/// <param name="Accent">The accent choice (see <see cref="AccentChoice"/>).</param>
public sealed record UserPreferences(AppLanguage Language = AppLanguage.System, string Accent = AccentChoice.Default)
{
    /// <summary>Gets the preferences of a user who never changed anything.</summary>
    public static UserPreferences Default { get; } = new();
}

/// <summary>Persists <see cref="UserPreferences"/>.</summary>
public interface IPreferencesStore
{
    /// <summary>Loads the preferences.</summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The saved preferences, or <see cref="UserPreferences.Default"/> when they are missing or unreadable; never throws for those cases.</returns>
    Task<UserPreferences> LoadAsync(CancellationToken ct);

    /// <summary>Saves the preferences.</summary>
    /// <param name="preferences">The preferences to save.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <exception cref="Yaapic.Core.Storage.StorageException">The store cannot be written.</exception>
    Task SaveAsync(UserPreferences preferences, CancellationToken ct);
}
