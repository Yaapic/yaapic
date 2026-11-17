using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Yaapic.App.Avalonia.Services;
using Yaapic.Domain.Http;
using Yaapic.Localization;
using Yaapic.UI.Abstractions;
using Yaapic.ViewModels.Settings;

namespace Yaapic.App.Avalonia.Views;

/// <summary>Modal window that lists, edits, orders and saves the client certificates through a <see cref="CertificatesViewModel"/>.</summary>
public sealed partial class CertificatesWindow : Window
{
    private static readonly ClientCertificateFormat[] Formats = [ClientCertificateFormat.Pfx, ClientCertificateFormat.Pem];

    private readonly CertificatesViewModel? _viewModel;
    private readonly Func<Window, Task>? _showVault;
    private CertificateItemViewModel? _tracked;
    private bool _syncingFormat;
    private bool _closeConfirmed;
    private bool _closePending;

    /// <summary>Creates the window for the previewer; it has no view model.</summary>
    public CertificatesWindow() => InitializeComponent();

    /// <summary>Creates the window.</summary>
    /// <param name="viewModel">The loaded certificates.</param>
    /// <param name="showVault">Opens the vault window over the given owner and completes when it is closed.</param>
    public CertificatesWindow(CertificatesViewModel viewModel, Func<Window, Task> showVault) : this()
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        ArgumentNullException.ThrowIfNull(showVault);
        _viewModel = viewModel;
        _showVault = showVault;
        DataContext = viewModel;
        viewModel.PropertyChanged += OnViewModelChanged;
        Track(viewModel.Selected);
        Closed += (_, _) =>
        {
            viewModel.PropertyChanged -= OnViewModelChanged;
            Track(null);
        };
    }

    /// <inheritdoc />
    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_closeConfirmed || _viewModel is not { IsDirty: true } vm)
        {
            return;
        }

        e.Cancel = true;
        if (_closePending)
        {
            return;
        }

        _closePending = true;
        try
        {
            switch (await new AvaloniaDialogService(() => this).ConfirmUnsavedAsync(Strings.CertificatesTitle))
            {
                case UnsavedChoice.Save:
                    await vm.SaveCommand.ExecuteAsync(null);
                    _closeConfirmed = !vm.IsDirty;
                    break;
                case UnsavedChoice.Discard:
                    _closeConfirmed = true;
                    break;
            }
        }
        finally
        {
            _closePending = false;
        }

        if (_closeConfirmed)
        {
            Close();
        }
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CertificatesViewModel.Selected))
        {
            Track(_viewModel!.Selected);
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

    // Index based picker: no enum boxing through the bindings under Native AOT.
    private void SyncFormat()
    {
        _syncingFormat = true;
        FormatBox.SelectedIndex = _tracked is null ? -1 : Math.Max(0, Array.IndexOf(Formats, _tracked.Format));
        _syncingFormat = false;
    }

    private void OnFormatChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_syncingFormat && _tracked is not null && FormatBox.SelectedIndex >= 0)
        {
            _tracked.Format = Formats[FormatBox.SelectedIndex];
        }
    }

    private async void OnUnlock(object? sender, RoutedEventArgs e)
    {
        if (_showVault is not null)
        {
            await _showVault(this);
        }
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
