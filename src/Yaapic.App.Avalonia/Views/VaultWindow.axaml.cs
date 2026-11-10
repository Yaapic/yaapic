using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Yaapic.Core.Secrets;
using Yaapic.ViewModels.Vault;

namespace Yaapic.App.Avalonia.Views;

/// <summary>Modal window that sets up a new vault or unlocks the existing one, chosen by the vault state when it is created.</summary>
public sealed partial class VaultWindow : Window
{
    private readonly VaultViewModel? _viewModel;
    private readonly bool _isSetup;

    /// <summary>Creates the window for the previewer; it has no view model.</summary>
    public VaultWindow() => InitializeComponent();

    /// <summary>Creates the window.</summary>
    /// <param name="viewModel">The vault view model; its state at this point selects the setup or unlock form.</param>
    public VaultWindow(VaultViewModel viewModel) : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;
        _isSetup = viewModel.State == VaultState.NotCreated;
        Title = _isSetup ? "Set up the secret vault" : "Unlock the secret vault";
        SubmitButton.Content = _isSetup ? "Create vault" : "Unlock";
        SetupPanel.IsVisible = _isSetup;
        Unlock.IsVisible = !_isSetup;
        foreach (var choice in viewModel.Protectors)
        {
            Protectors.Children.Add(CreateRadio(choice));
        }

        Passphrase.TextChanged += (_, _) => viewModel.Passphrase = Passphrase.Text ?? "";
        Confirm.TextChanged += (_, _) => viewModel.ConfirmPassphrase = Confirm.Text ?? "";
        Unlock.TextChanged += (_, _) => viewModel.UnlockPassphrase = Unlock.Text ?? "";
        viewModel.PropertyChanged += OnViewModelChanged;
        // A successful setup or unlock changes the state: that is the signal to close.
        viewModel.StateChanged += OnStateChanged;
        Refresh();
        Opened += (_, _) => (_isSetup ? Passphrase : Unlock).Focus();
        Closed += OnWindowClosed;
    }

    private RadioButton CreateRadio(VaultProtectorChoice choice)
    {
        var radio = new RadioButton
        {
            GroupName = "protector",
            Content = choice.RequiresPassphrase ? $"{choice.Label} (you will type it each time you open the workspace)" : choice.Label,
            IsChecked = choice == _viewModel!.SelectedProtector,
        };
        radio.IsCheckedChanged += (_, _) =>
        {
            if (radio.IsChecked == true)
            {
                _viewModel.SelectedProtector = choice;
            }
        };
        return radio;
    }

    private void OnViewModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Refresh();

    private void OnStateChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(Close);

    private void Refresh()
    {
        var vm = _viewModel!;
        PassphrasePanel.IsVisible = vm.SelectedProtector is { RequiresPassphrase: true };
        HintText.Text = vm.SetupHint;
        HintText.IsVisible = vm.SetupHint.Length > 0;
        ErrorText.Text = vm.LastError;
        ErrorText.IsVisible = !string.IsNullOrEmpty(vm.LastError);
        // The view model clears the field after a wrong passphrase: mirror it.
        if (Unlock.Text != vm.UnlockPassphrase && vm.UnlockPassphrase.Length == 0)
        {
            Unlock.Text = "";
        }

        SubmitButton.IsEnabled = !vm.IsBusy && (_isSetup ? vm.CanSubmitSetup : vm.UnlockPassphrase.Length > 0);
    }

    // The window stays open on an error; the state change closes it on success.
    private void OnSubmit(object? sender, RoutedEventArgs e) =>
        (_isSetup ? _viewModel!.SetupCommand : _viewModel!.UnlockCommand).Execute(null);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close();

    private void OnWindowClosed(object? sender, EventArgs e)
    {
        var vm = _viewModel!;
        vm.PropertyChanged -= OnViewModelChanged;
        vm.StateChanged -= OnStateChanged;
        Passphrase.Text = "";
        Confirm.Text = "";
        Unlock.Text = "";
        vm.Passphrase = "";
        vm.ConfirmPassphrase = "";
        vm.UnlockPassphrase = "";
        vm.LastError = null;
    }
}
