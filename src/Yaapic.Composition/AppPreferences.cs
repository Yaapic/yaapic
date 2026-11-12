using System.Globalization;
using Yaapic.Core.Preferences;
using Yaapic.Infrastructure.Preferences;
using Yaapic.Localization;

namespace Yaapic.Composition;

/// <summary>Loads the per-user preferences at start-up and applies the language, before any window or view model exists.</summary>
public static class AppPreferences
{
    /// <summary>Creates the preferences store.</summary>
    /// <param name="userDataDirectory">Another per-user data directory (screenshot captures); <see langword="null"/> for the real one.</param>
    /// <returns>The store.</returns>
    public static IPreferencesStore CreateStore(string? userDataDirectory = null) => new FilePreferencesStore(userDataDirectory);

    /// <summary>Loads the preferences and sets the interface language from them; never throws, a broken file gives the defaults.</summary>
    /// <param name="store">The preferences store.</param>
    /// <param name="systemCulture">The system UI culture, or <see langword="null"/> for the current one.</param>
    /// <returns>The preferences that were loaded.</returns>
    public static UserPreferences LoadAndApplyLanguage(IPreferencesStore store, CultureInfo? systemCulture = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        var preferences = UserPreferences.Default;
        try
        {
            // The file is a few bytes read once at start-up, so blocking on it here is cheaper than making start-up asynchronous.
            preferences = Task.Run(() => store.LoadAsync(CancellationToken.None)).GetAwaiter().GetResult();
        }
        catch (Core.Storage.StorageException)
        {
            // Defaults.
        }

        Localizer.Use(LanguageResolver.Resolve(preferences.Language, systemCulture ?? CultureInfo.CurrentUICulture));
        return preferences;
    }
}
