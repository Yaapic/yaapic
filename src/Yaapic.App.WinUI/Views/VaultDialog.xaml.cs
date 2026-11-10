using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.Core.Secrets;
using Yaapic.ViewModels.Vault;

namespace Yaapic.App.WinUI.Views;

/// <summary>Dialog that sets up a new vault or unlocks the existing one, chosen by the vault state when it is created.</summary>
public sealed partial class VaultDialog : ContentDialog
{
    private readonly bool _isSetup;
    private readonly EventHandler _onStateChanged;

    /// <summary>Initializes the dialog.</summary>
    /// <param name="viewModel">The vault view model; its state at this point selects the setup or unlock form.</param>
    public VaultDialog(VaultViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ViewModel = viewModel;
        _isSetup = viewModel.State == VaultState.NotCreated;
        InitializeComponent();
        Services.DialogButtons.Normalize(this);
        Title = _isSetup ? "Set up the secret vault" : "Unlock the secret vault";
        PrimaryButtonText = _isSetup ? "Create vault" : "Unlock";
        SetupPanel.Visibility = _isSetup ? Visibility.Visible : Visibility.Collapsed;
        Unlock.Visibility = _isSetup ? Visibility.Collapsed : Visibility.Visible;
        IsPrimaryButtonEnabled = CanSubmit();
        viewModel.PropertyChanged += (_, _) => IsPrimaryButtonEnabled = CanSubmit();
        // A successful setup or unlock changes the state: that is the signal to close.
        _onStateChanged = (_, _) => DispatcherQueue.TryEnqueue(Hide);
        viewModel.StateChanged += _onStateChanged;
        Loaded += (_, _) =>
        {
            Protectors.SelectedItem = viewModel.SelectedProtector;
            (_isSetup ? (Control)Protectors : Unlock).Focus(FocusState.Programmatic);
        };
    }

    /// <summary>Gets the vault view model.</summary>
    public VaultViewModel ViewModel { get; }

    private void OnProtectorChanged(object sender, SelectionChangedEventArgs e)
    {
        if (Protectors.SelectedItem is VaultProtectorChoice choice)
        {
            ViewModel.SelectedProtector = choice;
        }
    }

    private bool CanSubmit() => !ViewModel.IsBusy && (_isSetup ? ViewModel.CanSubmitSetup : ViewModel.UnlockPassphrase.Length > 0);

    // The dialog stays open on an error; the state change closes it on success.
    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        if (_isSetup)
        {
            ViewModel.SetupCommand.Execute(null);
        }
        else
        {
            ViewModel.UnlockCommand.Execute(null);
        }
    }

    private void OnClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
    {
        ViewModel.StateChanged -= _onStateChanged;
        Passphrase.Password = "";
        Confirm.Password = "";
        Unlock.Password = "";
        ViewModel.Passphrase = "";
        ViewModel.ConfirmPassphrase = "";
        ViewModel.UnlockPassphrase = "";
        ViewModel.LastError = null;
    }
}
