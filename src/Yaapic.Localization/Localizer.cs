using System.Globalization;

namespace Yaapic.Localization;

/// <summary>The languages the interface is translated to.</summary>
public enum UiLanguage
{
    /// <summary>English, the fallback language.</summary>
    English,

    /// <summary>French.</summary>
    French,
}

/// <summary>Holds the interface language and picks the text of a <see cref="Strings"/> member in it.</summary>
public static class Localizer
{
    /// <summary>Gets the current language. It is set once at start-up, before any window or view model exists.</summary>
    public static UiLanguage Current { get; private set; } = UiLanguage.English;

    /// <summary>Sets the language and, with it, the default culture of every thread (dates, numbers, sizes).</summary>
    /// <param name="language">The language to use.</param>
    public static void Use(UiLanguage language)
    {
        Current = language;
        var culture = CultureInfo.GetCultureInfo(language == UiLanguage.French ? "fr-FR" : "en-US");
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>Gets the language of a culture: French for any French culture, English otherwise.</summary>
    /// <param name="culture">The culture, for example the system UI culture.</param>
    /// <returns>The matching language.</returns>
    public static UiLanguage FromCulture(CultureInfo culture)
    {
        ArgumentNullException.ThrowIfNull(culture);
        return culture.TwoLetterISOLanguageName == "fr" ? UiLanguage.French : UiLanguage.English;
    }

    /// <summary>Picks the text for the current language; a missing French text falls back to English.</summary>
    /// <param name="english">The English text.</param>
    /// <param name="french">The French text, or <see langword="null"/> when it is not translated yet.</param>
    /// <returns>The text to show.</returns>
    public static string Pick(string english, string? french) =>
        Current == UiLanguage.French && french is not null ? french : english;
}
