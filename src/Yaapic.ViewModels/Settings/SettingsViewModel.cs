using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Preferences;
using Yaapic.Core.Storage;
using Yaapic.Localization;

namespace Yaapic.ViewModels.Settings;

/// <summary>One accent preset the user can pick.</summary>
/// <param name="Id">The preset id, as stored in the preferences.</param>
/// <param name="Rgb">The colour as <c>0xRRGGBB</c>, for the swatch.</param>
public sealed record AccentSwatch(string Id, int Rgb);

/// <summary>The language and accent settings: loads and saves the per-user preferences and tells the UI what to apply.</summary>
public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IPreferencesStore _store;
    private readonly UiLanguage _runningLanguage;
    private UserPreferences _preferences = UserPreferences.Default;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRestartNeeded))]
    private AppLanguage _language;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSystemAccent))]
    [NotifyPropertyChangedFor(nameof(IsCustomAccent))]
    private string _accent = AccentChoice.Default;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomValid))]
    private string _customAccentText = "";

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Creates the view model.</summary>
    /// <param name="store">The preferences store.</param>
    /// <param name="systemCulture">The system UI culture, used to resolve the "system" language; <see langword="null"/> for the current one.</param>
    public SettingsViewModel(IPreferencesStore store, System.Globalization.CultureInfo? systemCulture = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _systemCulture = systemCulture ?? System.Globalization.CultureInfo.CurrentUICulture;
        _runningLanguage = Localizer.Current;
        Presets = [.. AccentChoice.Presets.Select(id => new AccentSwatch(id, AccentPalette.PresetColor(id)))];
    }

    private readonly System.Globalization.CultureInfo _systemCulture;
    private bool _loading;

    /// <summary>Raised after an accent choice is saved: the UI applies it to the running app at once.</summary>
    public event EventHandler<string>? AccentChanged;

    /// <summary>Gets the accent presets, in display order.</summary>
    public IReadOnlyList<AccentSwatch> Presets { get; }

    /// <summary>Gets the languages the user can pick.</summary>
    public IReadOnlyList<AppLanguage> Languages { get; } = [AppLanguage.System, AppLanguage.English, AppLanguage.French];

    /// <summary>Gets a value indicating whether the accent follows the operating system.</summary>
    public bool IsSystemAccent => Accent == AccentChoice.System;

    /// <summary>Gets a value indicating whether the accent is a custom colour rather than a preset or the system accent.</summary>
    public bool IsCustomAccent => AccentChoice.TryParseCustom(Accent, out _);

    /// <summary>Gets a value indicating whether the text typed for a custom colour is a valid <c>#RRGGBB</c> value.</summary>
    public bool IsCustomValid => AccentChoice.TryParseCustom(CustomAccentText.Trim(), out _);

    /// <summary>Gets a value indicating whether the chosen language differs from the one the app started with: it applies after a restart.</summary>
    public bool IsRestartNeeded => LanguageResolver.Resolve(Language, _systemCulture) != _runningLanguage;

    /// <summary>Loads the saved preferences.</summary>
    /// <returns>A task completing when they are loaded.</returns>
    public async Task InitializeAsync()
    {
        _loading = true;
        try
        {
            _preferences = await _store.LoadAsync(CancellationToken.None);
            Language = _preferences.Language;
            Accent = _preferences.Accent;
            CustomAccentText = IsCustomAccent ? _preferences.Accent : "";
        }
        finally
        {
            _loading = false;
        }
    }

    /// <summary>Picks a preset or the system accent and applies it.</summary>
    /// <param name="id">A preset id or <see cref="AccentChoice.System"/>.</param>
    [RelayCommand]
    private Task ChooseAccentAsync(string id) => SetAccentAsync(id);

    /// <summary>Applies the custom colour typed by the user; does nothing while the text is not a valid colour.</summary>
    [RelayCommand(CanExecute = nameof(IsCustomValid))]
    private Task ApplyCustomAccentAsync() => SetAccentAsync(CustomAccentText.Trim().ToUpperInvariant());

    partial void OnLanguageChanged(AppLanguage value)
    {
        if (!_loading)
        {
            _ = SaveAsync(_preferences with { Language = value });
        }
    }

    partial void OnCustomAccentTextChanged(string value) => ApplyCustomAccentCommand.NotifyCanExecuteChanged();

    private async Task SetAccentAsync(string choice)
    {
        if (!AccentChoice.IsValid(choice))
        {
            return;
        }

        Accent = choice;
        await SaveAsync(_preferences with { Accent = choice });
        AccentChanged?.Invoke(this, choice);
    }

    private async Task SaveAsync(UserPreferences preferences)
    {
        _preferences = preferences;
        ErrorMessage = null;
        try
        {
            await _store.SaveAsync(preferences, CancellationToken.None);
        }
        catch (StorageException ex)
        {
            ErrorMessage = ex.Message;
        }
    }
}
