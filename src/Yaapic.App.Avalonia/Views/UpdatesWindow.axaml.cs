using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Yaapic.Core.Preferences;
using Yaapic.Localization;
using Yaapic.ViewModels.Settings;
using Yaapic.ViewModels.Updates;

namespace Yaapic.App.Avalonia.Views;

/// <summary>The settings window: language, accent colour, and the update preferences with the state of the update.</summary>
public sealed partial class UpdatesWindow : Window
{
    private readonly SettingsViewModel? _settings;
    private readonly Action? _restart;
    private readonly Dictionary<string, Button> _swatchButtons = [];
    private bool _loading = true;

    /// <summary>Creates the window for the previewer; it has no view model.</summary>
    public UpdatesWindow() => InitializeComponent();

    /// <summary>Creates the window.</summary>
    /// <param name="viewModel">The update view model.</param>
    /// <param name="settings">The language and accent settings.</param>
    /// <param name="restart">Restarts the app, offered when the language changed.</param>
    public UpdatesWindow(UpdateViewModel viewModel, SettingsViewModel settings, Action restart) : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(restart);
        DataContext = viewModel;
        _settings = settings;
        _restart = restart;
        Title = Strings.SettingsTitle;
        VersionText.Text = Strings.CommonVersion(viewModel.CurrentVersion);
        TaglineText.Text = Strings.SettingsTagline;
        AppearanceTitle.Text = Strings.SettingsAppearance;
        LanguageLabel.Text = Strings.SettingsLanguage;
        foreach (var (language, label) in new[]
        {
            (AppLanguage.System, Strings.SettingsLanguageSystem),
            (AppLanguage.English, Strings.SettingsLanguageEnglish),
            (AppLanguage.French, Strings.SettingsLanguageFrench),
        })
        {
            LanguageBox.Items.Add(new ComboBoxItem { Content = label, Tag = language });
        }

        RestartNote.Text = Strings.SettingsRestartNote;
        RestartButton.Content = Strings.SettingsRestartNow;
        AccentTitle.Text = Strings.SettingsAccent;
        CustomBox.PlaceholderText = Strings.SettingsCustomColor;
        ApplyButton.Content = Strings.SettingsApply;
        CustomError.Text = Strings.SettingsInvalidColor;
        UpdatesTitle.Text = Strings.SettingsUpdates;
        BuildSwatches();
        SyncFromSettings();
        settings.PropertyChanged += OnSettingsChanged;
        Closed += (_, _) => settings.PropertyChanged -= OnSettingsChanged;
        _loading = false;
    }

    private void BuildSwatches()
    {
        foreach (var swatch in _settings!.Presets)
        {
            AddSwatch(swatch.Id, AccentNames.Of(swatch.Id), new SolidColorBrush(Color.FromRgb((byte)(swatch.Rgb >> 16), (byte)(swatch.Rgb >> 8), (byte)swatch.Rgb)));
        }

        // The operating system accent: a hollow swatch, since its colour is only known to the platform.
        AddSwatch(AccentChoice.System, Strings.SettingsAccentSystem, Brushes.Transparent);
    }

    private void AddSwatch(string id, string name, IBrush fill)
    {
        var button = new Button
        {
            Width = 28,
            Height = 28,
            MinWidth = 28,
            MinHeight = 28,
            Padding = new global::Avalonia.Thickness(0),
            CornerRadius = new global::Avalonia.CornerRadius(14),
            Background = fill,
            BorderThickness = new global::Avalonia.Thickness(2),
        };
        ToolTip.SetTip(button, name);
        global::Avalonia.Automation.AutomationProperties.SetName(button, name);
        button.Click += async (_, _) => await _settings!.ChooseAccentCommand.ExecuteAsync(id);
        _swatchButtons[id] = button;
        Swatches.Children.Add(button);
    }

    private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => SyncFromSettings();

    private void SyncFromSettings()
    {
        if (_settings is null)
        {
            return;
        }

        _loading = true;
        LanguageBox.SelectedIndex = Array.IndexOf([.. _settings.Languages], _settings.Language);
        RestartPanel.IsVisible = _settings.IsRestartNeeded;
        foreach (var (id, button) in _swatchButtons)
        {
            var key = id == _settings.Accent ? "YaapicTextPrimaryBrush" : "YaapicBorderDefaultBrush";
            button.BorderBrush = this.TryFindResource(key, ActualThemeVariant, out var brush) ? brush as IBrush : null;
        }

        if (_settings.IsCustomAccent && CustomBox.Text != _settings.CustomAccentText)
        {
            CustomBox.Text = _settings.CustomAccentText;
        }

        ApplyButton.IsEnabled = _settings.ApplyCustomAccentCommand.CanExecute(null);
        CustomError.IsVisible = _settings.CustomAccentText.Length > 0 && !_settings.IsCustomValid;
        _loading = false;
    }

    private void OnLanguageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_loading && _settings is not null && LanguageBox.SelectedItem is ComboBoxItem { Tag: AppLanguage language })
        {
            _settings.Language = language;
        }
    }

    private void OnCustomChanged(object? sender, TextChangedEventArgs e)
    {
        if (!_loading && _settings is not null)
        {
            _settings.CustomAccentText = CustomBox.Text ?? "";
        }
    }

    private async void OnApplyCustomClick(object? sender, RoutedEventArgs e)
    {
        if (_settings is not null)
        {
            await _settings.ApplyCustomAccentCommand.ExecuteAsync(null);
        }
    }

    private void OnRestartClick(object? sender, RoutedEventArgs e) => _restart?.Invoke();

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
