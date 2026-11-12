using System.Globalization;
using Yaapic.Composition;
using Yaapic.Core.Preferences;
using Yaapic.Localization;
using Yaapic.ViewModels.Settings;
using Yaapic.ViewModels.Theming;

namespace Yaapic.Infrastructure.Tests.Composition;

// The language is process-wide: this class shares the collection of the other tests that change it.
[Collection("Localizer")]
public sealed class PersonalizationEndToEndTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "yaapic-e2e-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        Localizer.Use(UiLanguage.English);
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_preferences_file_with_french_and_a_custom_accent_drives_the_start_up()
    {
        Directory.CreateDirectory(Path.Combine(_dir, "Yaapic"));
        await File.WriteAllTextAsync(Path.Combine(_dir, "Yaapic", "preferences.json"), """{"language":"french","accent":"#336699"}""", Ct);

        var preferences = AppPreferences.LoadAndApplyLanguage(AppPreferences.CreateStore(_dir), CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal("Paramètres", Strings.SettingsTitle);
        Assert.Equal("fr", CultureInfo.CurrentCulture.TwoLetterISOLanguageName);
        var dark = AccentPalette.ForChoice(preferences.Accent, dark: true)!.Value;
        Assert.Equal(0x336699, dark.Fill);
        Assert.Contains(AccentResources.Entries(winUi: true, dark), r => r.Key == "AccentButtonBackground" && r.Rgb == 0x336699);
        Assert.Contains(AccentResources.Entries(winUi: false, dark), r => r.Key == "SystemAccentColor" && r.Rgb == 0x336699);
    }

    [Fact]
    public async Task Choices_made_in_the_settings_survive_a_restart()
    {
        var store = AppPreferences.CreateStore(_dir);
        var settings = new SettingsViewModel(store, CultureInfo.GetCultureInfo("en-US"));
        await settings.InitializeAsync();

        await settings.ChooseAccentCommand.ExecuteAsync("green");
        settings.Language = AppLanguage.French;
        await Task.Delay(100, Ct);

        var restarted = AppPreferences.LoadAndApplyLanguage(AppPreferences.CreateStore(_dir), CultureInfo.GetCultureInfo("en-US"));

        Assert.Equal(new UserPreferences(AppLanguage.French, "green"), restarted);
        Assert.Equal(UiLanguage.French, Localizer.Current);
    }

    [Fact]
    public void A_missing_file_starts_in_the_system_language_with_the_teal_accent()
    {
        var preferences = AppPreferences.LoadAndApplyLanguage(AppPreferences.CreateStore(_dir), CultureInfo.GetCultureInfo("de-DE"));

        Assert.Equal(UserPreferences.Default, preferences);
        Assert.Equal(UiLanguage.English, Localizer.Current);
        Assert.Equal("Settings", Strings.SettingsTitle);
    }
}
