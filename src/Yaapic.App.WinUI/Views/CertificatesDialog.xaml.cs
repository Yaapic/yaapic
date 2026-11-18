using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Yaapic.Core.Secrets;
using Yaapic.Domain.Http;
using Yaapic.Localization;
using Yaapic.UI.Abstractions;
using Yaapic.ViewModels.Settings;

namespace Yaapic.App.WinUI.Views;

/// <summary>Dialog that lists, edits, orders and saves the client certificates through a <see cref="CertificatesViewModel"/>.</summary>
public sealed partial class CertificatesDialog : ContentDialog
{
    private static readonly ClientCertificateFormat[] Formats = [ClientCertificateFormat.Pfx, ClientCertificateFormat.Pem];

    private CertificateItemViewModel? _tracked;
    private bool _syncingFormat;

    private CertificatesDialog(CertificatesViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        Services.DialogButtons.Normalize(this, new Thickness(0, 10, 16, 10));
        viewModel.PropertyChanged += OnViewModelChanged;
        Closed += (_, _) =>
        {
            viewModel.PropertyChanged -= OnViewModelChanged;
            Track(null);
        };
        Loaded += (_, _) =>
        {
            Fit();
            XamlRoot.Changed += (_, _) => Fit();
        };
        Track(viewModel.Selected);
    }

    /// <summary>Gets the managed certificates.</summary>
    public CertificatesViewModel ViewModel { get; }

    /// <summary>Shows the dialog until the user closes it; a delete confirmation, the vault dialog and the unsaved-changes prompt are shown in between.</summary>
    /// <param name="viewModel">The loaded certificates.</param>
    /// <param name="dialogs">The dialog service, used for the unsaved-changes prompt.</param>
    /// <param name="showVault">Opens the vault dialog and completes when it is closed.</param>
    /// <param name="xamlRoot">The XAML root of the window.</param>
    /// <param name="theme">The theme to apply.</param>
    /// <returns>A task that completes when the dialog is closed for good.</returns>
    public static async Task RunAsync(CertificatesViewModel viewModel, IDialogService dialogs, Func<Task> showVault, XamlRoot xamlRoot, ElementTheme theme)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(showVault);
        ArgumentNullException.ThrowIfNull(xamlRoot);
        while (true)
        {
            var dialog = new CertificatesDialog(viewModel) { XamlRoot = xamlRoot, RequestedTheme = theme };
            await dialog.ShowAsync();
            switch (dialog._pending)
            {
                case Pending.None:
                    return;
                case Pending.Remove:
                    await viewModel.RemoveCommand.ExecuteAsync(null);
                    break;
                case Pending.Unlock:
                    await showVault();
                    break;
                default:
                    if (await ConfirmCloseAsync(viewModel, dialogs))
                    {
                        return;
                    }

                    break;
            }
        }
    }

    // WinUI shows one ContentDialog at a time: work that needs another dialog runs once this one is hidden, then it comes back.
    private enum Pending
    {
        None,
        Remove,
        Unlock,
        Close,
    }

    private Pending _pending;

    private static async Task<bool> ConfirmCloseAsync(CertificatesViewModel viewModel, IDialogService dialogs)
    {
        switch (await dialogs.ConfirmUnsavedAsync(Strings.CertificatesTitle))
        {
            case UnsavedChoice.Save:
                await viewModel.SaveCommand.ExecuteAsync(null);
                return !viewModel.IsDirty;
            case UnsavedChoice.Discard:
                return true;
            default:
                return false;
        }
    }

    private bool IsVaultNotUnlocked(VaultState state) => state != VaultState.Unlocked;

    private string PassphrasePlaceholder(bool hasPassphrase) => hasPassphrase ? Strings.CertificatesKeepCurrent : "";

    // Keeps the dialog clear of the title bar when the window is small.
    private void Fit()
    {
        var size = XamlRoot.Size;
        Body.Width = Math.Clamp(size.Width - 64, 320, 840);
        Body.Height = Math.Clamp(size.Height - 2 * 56 - 52, 96, 520);
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CertificatesViewModel.Selected))
        {
            Track(ViewModel.Selected);
        }
    }

    private void Track(CertificateItemViewModel? item)
    {
        if (_tracked is not null)
        {
            _tracked.PropertyChanged -= OnItemChanged;
        }

        _tracked = item;
        if (item is not null)
        {
            item.PropertyChanged += OnItemChanged;
        }

        SyncFormat();
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CertificateItemViewModel.Format))
        {
            SyncFormat();
        }
    }

    // Index based picker: boxed managed enums cannot cross into XAML under Native AOT.
    private void SyncFormat()
    {
        _syncingFormat = true;
        FormatBox.SelectedIndex = _tracked is null ? -1 : Math.Max(0, Array.IndexOf(Formats, _tracked.Format));
        _syncingFormat = false;
    }

    private void OnFormatChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_syncingFormat && _tracked is not null && FormatBox.SelectedIndex >= 0)
        {
            _tracked.Format = Formats[FormatBox.SelectedIndex];
        }
    }

    // The footer Save keeps the dialog open.
    private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        args.Cancel = true;
        ViewModel.SaveCommand.Execute(null);
    }

    private void OnCloseButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (ViewModel.IsDirty)
        {
            _pending = Pending.Close;
        }
    }

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        _pending = Pending.Remove;
        Hide();
    }

    private void OnUnlock(object sender, RoutedEventArgs e)
    {
        _pending = Pending.Unlock;
        Hide();
    }
}
