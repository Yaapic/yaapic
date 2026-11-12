using System.Globalization;

namespace Yaapic.Localization.Tests;

// The language is process-wide: tests that change it share one collection so they never run side by side.
[Collection("Localizer")]
public sealed class LocalizerTests : IDisposable
{
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public void Dispose()
    {
        Localizer.Use(UiLanguage.English);
        CultureInfo.CurrentCulture = _culture;
    }

    [Fact]
    public void French_gets_the_french_text_and_culture()
    {
        Localizer.Use(UiLanguage.French);

        Assert.Equal("Anglais", Strings.LanguageEnglish);
        Assert.Equal("fr", CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
    }

    [Fact]
    public void English_gets_the_english_text()
    {
        Localizer.Use(UiLanguage.English);

        Assert.Equal("English", Strings.LanguageEnglish);
        Assert.Equal("Version 1.2", Strings.CommonVersion("1.2"));
    }

    [Theory]
    [InlineData("fr-FR", UiLanguage.French)]
    [InlineData("fr-CA", UiLanguage.French)]
    [InlineData("en-GB", UiLanguage.English)]
    [InlineData("de-DE", UiLanguage.English)]
    public void The_language_follows_the_culture(string name, UiLanguage expected) =>
        Assert.Equal(expected, Localizer.FromCulture(CultureInfo.GetCultureInfo(name)));

    [Fact]
    public void A_missing_french_text_falls_back()
    {
        Localizer.Use(UiLanguage.French);

        Assert.Equal("fallback", Localizer.Pick("fallback", null));
    }
}
