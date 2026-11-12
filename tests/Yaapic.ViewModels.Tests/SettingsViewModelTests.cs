using System.Globalization;
using Yaapic.Core.Preferences;
using Yaapic.Localization;
using Yaapic.TestDoubles;
using Yaapic.ViewModels.Settings;

namespace Yaapic.ViewModels.Tests;

[Collection("Localizer")]
public sealed class SettingsViewModelTests : IDisposable
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");
    private readonly InMemoryPreferencesStore _store = new();

    public SettingsViewModelTests() => Localizer.Use(UiLanguage.English);

    public void Dispose() => Localizer.Use(UiLanguage.English);

    private SettingsViewModel Create() => new(_store, English);

    [Fact]
    public async Task The_saved_preferences_are_loaded()
    {
        _store.Preferences = new UserPreferences(AppLanguage.French, "#336699");
        var vm = Create();

        await vm.InitializeAsync();

        Assert.Equal(AppLanguage.French, vm.Language);
        Assert.True(vm.IsCustomAccent);
        Assert.Equal("#336699", vm.CustomAccentText);
    }

    [Fact]
    public async Task Choosing_a_preset_saves_it_and_tells_the_ui()
    {
        var vm = Create();
        await vm.InitializeAsync();
        string? applied = null;
        vm.AccentChanged += (_, choice) => applied = choice;

        await vm.ChooseAccentCommand.ExecuteAsync("purple");

        Assert.Equal("purple", _store.Preferences.Accent);
        Assert.Equal("purple", applied);
        Assert.False(vm.IsCustomAccent);
    }

    [Fact]
    public async Task The_system_accent_can_be_chosen()
    {
        var vm = Create();
        await vm.InitializeAsync();

        await vm.ChooseAccentCommand.ExecuteAsync(AccentChoice.System);

        Assert.True(vm.IsSystemAccent);
        Assert.Equal("system", _store.Preferences.Accent);
    }

    [Fact]
    public async Task A_valid_custom_colour_is_applied_in_upper_case()
    {
        var vm = Create();
        await vm.InitializeAsync();
        vm.CustomAccentText = " #a1b2c3 ";

        Assert.True(vm.ApplyCustomAccentCommand.CanExecute(null));
        await vm.ApplyCustomAccentCommand.ExecuteAsync(null);

        Assert.Equal("#A1B2C3", _store.Preferences.Accent);
        Assert.True(vm.IsCustomAccent);
    }

    [Theory]
    [InlineData("")]
    [InlineData("#12345")]
    [InlineData("123456")]
    [InlineData("#GGGGGG")]
    public async Task An_invalid_custom_colour_cannot_be_applied(string text)
    {
        var vm = Create();
        await vm.InitializeAsync();
        vm.CustomAccentText = text;

        Assert.False(vm.IsCustomValid);
        Assert.False(vm.ApplyCustomAccentCommand.CanExecute(null));
        Assert.Equal(AccentChoice.Default, _store.Preferences.Accent);
    }

    [Fact]
    public async Task An_unknown_preset_is_ignored()
    {
        var vm = Create();
        await vm.InitializeAsync();

        await vm.ChooseAccentCommand.ExecuteAsync("chartreuse");

        Assert.Equal(AccentChoice.Default, vm.Accent);
    }

    [Fact]
    public async Task Changing_the_language_saves_it_and_asks_for_a_restart()
    {
        var vm = Create();
        await vm.InitializeAsync();
        Assert.False(vm.IsRestartNeeded);

        vm.Language = AppLanguage.French;
        await Task.Yield();

        Assert.True(vm.IsRestartNeeded);
        Assert.Equal(AppLanguage.French, _store.Preferences.Language);

        vm.Language = AppLanguage.English;
        Assert.False(vm.IsRestartNeeded);
    }

    [Fact]
    public async Task Loading_does_not_save_anything()
    {
        _store.Preferences = new UserPreferences(AppLanguage.French, "blue");
        var vm = Create();

        await vm.InitializeAsync();

        Assert.Equal(new UserPreferences(AppLanguage.French, "blue"), _store.Preferences);
    }

    [Fact]
    public void The_presets_match_the_palette() =>
        Assert.Equal(AccentChoice.Presets, Create().Presets.Select(p => p.Id));
}
