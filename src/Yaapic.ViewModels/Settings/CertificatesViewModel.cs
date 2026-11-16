using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Secrets;
using Yaapic.Core.Storage;
using Yaapic.Domain.Http;
using Yaapic.Localization;
using Yaapic.UI.Abstractions;

namespace Yaapic.ViewModels.Settings;

/// <summary>Lists, edits, orders and saves the client certificate entries; passphrases go to the vault, never to the store.</summary>
public sealed partial class CertificatesViewModel : ObservableObject
{
    private static readonly HashSet<string> NonEditNames =
    [
        nameof(CertificateItemViewModel.Issues),
        nameof(CertificateItemViewModel.IsKeyPathVisible),
        nameof(CertificateItemViewModel.IsValid),
        nameof(CertificateItemViewModel.HasPassphrase),
    ];

    private readonly ICertificateStore _store;
    private readonly ISecretVault _vault;
    private readonly IDialogService _dialogs;
    private readonly List<Guid> _removedIds = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand), nameof(MoveUpCommand), nameof(MoveDownCommand), nameof(BrowseCertificateCommand), nameof(BrowseKeyCommand))]
    private CertificateItemViewModel? _selected;

    [ObservableProperty]
    private bool _isDirty;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Creates the view model.</summary>
    /// <param name="store">The certificate store.</param>
    /// <param name="vault">The secret vault holding the passphrases.</param>
    /// <param name="dialogs">The dialog service.</param>
    public CertificatesViewModel(ICertificateStore store, ISecretVault vault, IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(vault);
        ArgumentNullException.ThrowIfNull(dialogs);
        _store = store;
        _vault = vault;
        _dialogs = dialogs;
        vault.StateChanged += (_, _) => OnPropertyChanged(nameof(VaultState));
    }

    /// <summary>Gets the entries, in priority order.</summary>
    public ObservableCollection<CertificateItemViewModel> Items { get; } = [];

    /// <summary>Gets the state of the vault, so the view can say it is locked.</summary>
    public VaultState VaultState => _vault.State;

    /// <summary>Loads the entries from the store and clears the dirty flag.</summary>
    /// <returns>A task that completes when the list is loaded.</returns>
    public async Task LoadAsync()
    {
        ErrorMessage = null;
        IReadOnlyList<ClientCertificate> loaded;
        try
        {
            loaded = await _store.LoadAsync(CancellationToken.None);
        }
        catch (StorageException ex)
        {
            ErrorMessage = ex.Message;
            return;
        }

        foreach (var item in Items)
        {
            item.PropertyChanged -= OnItemChanged;
        }

        Items.Clear();
        _removedIds.Clear();
        foreach (var certificate in loaded)
        {
            Items.Add(Track(new CertificateItemViewModel(certificate)));
        }

        Selected = Items.FirstOrDefault();
        IsDirty = false;
    }

    [RelayCommand]
    private void Add()
    {
        var item = Track(new CertificateItemViewModel(new ClientCertificate(
            Guid.NewGuid(), Strings.CertificateNewName, "", ClientCertificateFormat.Pfx, "", "", false)));
        Items.Add(item);
        Selected = item;
        IsDirty = true;
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task RemoveAsync()
    {
        var item = Selected!;
        if (!await _dialogs.ConfirmDeleteAsync(item.Name))
        {
            return;
        }

        var index = Items.IndexOf(item);
        item.PropertyChanged -= OnItemChanged;
        Items.RemoveAt(index);
        _removedIds.Add(item.Id);
        Selected = Items.Count == 0 ? null : Items[Math.Min(index, Items.Count - 1)];
        IsDirty = true;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ErrorMessage = null;
        if (Items.Any(i => !i.IsValid))
        {
            return;
        }

        if (Items.Any(i => i.Passphrase.Length > 0 || i.RemovePassphrase) && _vault.State != VaultState.Unlocked)
        {
            ErrorMessage = Strings.CertificateVaultLockedForPassphrase;
            return;
        }

        try
        {
            await _store.SaveAsync([.. Items.Select(i => i.ToModel() with { HasPassphrase = HasPassphraseAfterSave(i) })], CancellationToken.None);
            await ApplyPassphrasesAsync();
        }
        catch (Exception ex) when (ex is StorageException or VaultException)
        {
            ErrorMessage = ex.Message;
            return;
        }

        foreach (var item in Items)
        {
            item.HasPassphrase = HasPassphraseAfterSave(item);
            item.Passphrase = "";
            item.RemovePassphrase = false;
        }

        _removedIds.Clear();
        IsDirty = false;
    }

    [RelayCommand(CanExecute = nameof(CanMoveUp))]
    private void MoveUp() => Move(-1);

    [RelayCommand(CanExecute = nameof(CanMoveDown))]
    private void MoveDown() => Move(1);

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task BrowseCertificateAsync()
    {
        if (await _dialogs.PickFileAsync(Strings.CommonSelectFile) is { } path)
        {
            Selected!.CertificatePath = path;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private async Task BrowseKeyAsync()
    {
        if (await _dialogs.PickFileAsync(Strings.CommonSelectFile) is { } path)
        {
            Selected!.KeyPath = path;
        }
    }

    // A newly typed passphrase wins over the "remove" flag.
    private static bool HasPassphraseAfterSave(CertificateItemViewModel item) =>
        item.Passphrase.Length > 0 || (item.HasPassphrase && !item.RemovePassphrase);

    private bool HasSelection() => Selected is not null;

    private bool CanMoveUp() => Selected is not null && Items.IndexOf(Selected) > 0;

    private bool CanMoveDown() => Selected is not null && Items.IndexOf(Selected) is var i && i >= 0 && i < Items.Count - 1;

    private async Task ApplyPassphrasesAsync()
    {
        if (_vault.State == VaultState.Unlocked)
        {
            foreach (var id in _removedIds)
            {
                await _vault.RemoveOwnerAsync(SecretOwnerKind.Certificate, id, CancellationToken.None);
            }
        }

        foreach (var item in Items)
        {
            var secret = SecretRef.Certificate(item.Id);
            if (item.Passphrase.Length > 0)
            {
                await _vault.SetAsync(secret, item.Passphrase, CancellationToken.None);
            }
            else if (item.RemovePassphrase)
            {
                await _vault.RemoveAsync(secret, CancellationToken.None);
            }
        }
    }

    private void Move(int delta)
    {
        var index = Items.IndexOf(Selected!);
        Items.Move(index, index + delta);
        IsDirty = true;
        MoveUpCommand.NotifyCanExecuteChanged();
        MoveDownCommand.NotifyCanExecuteChanged();
    }

    private CertificateItemViewModel Track(CertificateItemViewModel item)
    {
        item.PropertyChanged += OnItemChanged;
        return item;
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is { } name && !NonEditNames.Contains(name))
        {
            IsDirty = true;
        }
    }
}
