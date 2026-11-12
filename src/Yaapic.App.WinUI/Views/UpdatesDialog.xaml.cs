using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Yaapic.Core.Preferences;
using Yaapic.Core.Updates;
using Yaapic.Localization;
using Yaapic.ViewModels.Settings;
using Yaapic.ViewModels.Updates;

namespace Yaapic.App.WinUI.Views;

/// <summary>The settings dialog: language, accent colour, and the update preferences with the state of the update.</summary>
public sealed partial class UpdatesDialog : ContentDialog
{
    private readonly SettingsViewModel _settings;
    private readonly Action _restart;
    private readonly Dictionary<string, Button> _swatchButtons = [];
    private bool _loading = true;

    /// <summary>Initializes the dialog.</summary>
    /// <param name="viewModel">The update view model.</param>
    /// <param name="settings">The language and accent settings.</param>
    /// <param name="restart">Restarts the app, offered when the language changed.</param>
    public UpdatesDialog(UpdateViewModel viewModel, SettingsViewModel settings, Action restart)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(restart);
        ViewModel = viewModel;
        _settings = settings;
        _restart = restart;
        InitializeComponent();
        Services.DialogButtons.Normalize(this);
        Title = Strings.SettingsTitle;
        VersionText.Text = Strings.CommonVersion(viewModel.CurrentVersion);
        TaglineText.Text = "A fast, local-first API client. No account, no telemetry.";
        AppearanceTitle.Text = Strings.SettingsAppearance;
        LanguageBox.Header = Strings.SettingsLanguage;
        AutomationProperties.SetName(LanguageBox, Strings.SettingsLanguage);
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
        AutomationProperties.SetName(CustomBox, Strings.SettingsCustomColor);
        ApplyButton.Content = Strings.SettingsApply;
        CustomError.Text = Strings.SettingsInvalidColor;
        UpdatesTitle.Text = Strings.SettingsUpdates;
        BuildSwatches();
        SyncFromSettings();
        _settings.PropertyChanged += OnSettingsChanged;
        Closed += (_, _) => _settings.PropertyChanged -= OnSettingsChanged;

        ChannelBox.SelectedIndex = viewModel.Channel == AppChannel.Preview ? 1 : 0;
        ChannelBox.IsEnabled = viewModel.IsSupported;
        _loading = false;
    }

    /// <summary>Gets the update view model.</summary>
    public UpdateViewModel ViewModel { get; }

    private void BuildSwatches()
    {
        foreach (var swatch in _settings.Presets)
        {
            AddSwatch(swatch.Id, AccentNames.Of(swatch.Id), new SolidColorBrush(Color.FromArgb(0xFF, (byte)(swatch.Rgb >> 16), (byte)(swatch.Rgb >> 8), (byte)swatch.Rgb)));
        }

        // The operating system accent: a hollow swatch, since its colour is only known to the platform.
        AddSwatch(AccentChoice.System, Strings.SettingsAccentSystem, new SolidColorBrush(Microsoft.UI.Colors.Transparent));
    }

    private void AddSwatch(string id, string name, Brush fill)
    {
        var button = new Button
        {
            Width = 28,
            Height = 28,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(14),
            Background = fill,
            BorderThickness = new Thickness(2),
            Tag = id,
        };
        ToolTipService.SetToolTip(button, name);
        AutomationProperties.SetName(button, name);
        button.Click += async (_, _) => await _settings.ChooseAccentCommand.ExecuteAsync(id);
        _swatchButtons[id] = button;
        Swatches.Children.Add(button);
    }

    private void OnSettingsChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => SyncFromSettings();

    private void SyncFromSettings()
    {
        _loading = true;
        LanguageBox.SelectedIndex = Array.IndexOf([.. _settings.Languages], _settings.Language);
        RestartPanel.Visibility = _settings.IsRestartNeeded ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (id, button) in _swatchButtons)
        {
            var selected = id == _settings.Accent;
            button.BorderBrush = (Brush)Resources[selected ? "SwatchSelectedBrush" : "SwatchIdleBrush"];
        }

        if (_settings.IsCustomAccent && CustomBox.Text != _settings.CustomAccentText)
        {
            CustomBox.Text = _settings.CustomAccentText;
        }

        ApplyButton.IsEnabled = _settings.ApplyCustomAccentCommand.CanExecute(null);
        CustomError.Visibility = _settings.CustomAccentText.Length > 0 && !_settings.IsCustomValid ? Visibility.Visible : Visibility.Collapsed;
        _loading = false;
    }

    private void OnLanguageChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading && LanguageBox.SelectedItem is ComboBoxItem { Tag: AppLanguage language })
        {
            _settings.Language = language;
        }
    }

    private void OnCustomChanged(object sender, TextChangedEventArgs e)
    {
        if (!_loading)
        {
            _settings.CustomAccentText = CustomBox.Text;
        }
    }

    private async void OnApplyCustomClick(object sender, RoutedEventArgs e) => await _settings.ApplyCustomAccentCommand.ExecuteAsync(null);

    private void OnRestartClick(object sender, RoutedEventArgs e) => _restart();

    private void OnChannelChanged(object sender, SelectionChangedEventArgs e) =>
        ViewModel.Channel = ChannelBox.SelectedIndex == 1 ? AppChannel.Preview : AppChannel.Stable;
}
