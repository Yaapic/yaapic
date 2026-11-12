using System.Globalization;
using Yaapic.Composition;
using Yaapic.Core.Preferences;
using Yaapic.Localization;
using Yaapic.TestDoubles;

namespace Yaapic.Infrastructure.Tests.Composition;

// The language is process-wide: these tests share the collection of the localizer tests so they never run side by side.
[Collection("Localizer")]
public sealed class AppPreferencesTests : IDisposable
{
    public void Dispose() => Localizer.Use(UiLanguage.English);

    [Fact]
    public void A_saved_french_preference_sets_the_language()
    {
        var store = new InMemoryPreferencesStore { Preferences = new UserPreferences(AppLanguage.French, "blue") };

        var loaded = AppPreferences.LoadAndApplyLanguage(store, CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal("blue", loaded.Accent);
        Assert.Equal(UiLanguage.French, Localizer.Current);
    }

    [Fact]
    public void The_system_preference_follows_the_system_culture()
    {
        AppPreferences.LoadAndApplyLanguage(new InMemoryPreferencesStore(), CultureInfo.GetCultureInfo("fr-FR"));

        Assert.Equal(UiLanguage.French, Localizer.Current);
    }

    [Fact]
    public void Nothing_saved_gives_the_defaults()
    {
        var loaded = AppPreferences.LoadAndApplyLanguage(new InMemoryPreferencesStore(), CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal(UserPreferences.Default, loaded);
        Assert.Equal(UiLanguage.English, Localizer.Current);
    }
}
