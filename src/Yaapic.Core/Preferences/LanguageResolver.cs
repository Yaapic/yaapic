using System.Globalization;
using Yaapic.Localization;

namespace Yaapic.Core.Preferences;

/// <summary>Turns the language preference into the language to show.</summary>
public static class LanguageResolver
{
    /// <summary>Resolves a preference.</summary>
    /// <param name="choice">The preference.</param>
    /// <param name="systemCulture">The system UI culture, used for <see cref="AppLanguage.System"/>.</param>
    /// <returns>French or English.</returns>
    public static UiLanguage Resolve(AppLanguage choice, CultureInfo systemCulture) => choice switch
    {
        AppLanguage.French => UiLanguage.French,
        AppLanguage.English => UiLanguage.English,
        _ => Localizer.FromCulture(systemCulture),
    };
}
