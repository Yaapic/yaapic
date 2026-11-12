using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Secrets;
using Yaapic.Localization;

namespace Yaapic.ViewModels.Vault;

/// <summary>The state machine behind the vault UI: set up, unlock, lock, and reload the secret-bearing view models.</summary>
public sealed partial class VaultViewModel : ObservableObject, IVaultStateSource
{
    /// <summary>The minimum length of a new passphrase.</summary>
    public const int MinPassphraseLength = 8;

    private readonly ISecretVault _vault;
    private readonly IVaultReloader _reloader;
    private Task _pendingReload = Task.CompletedTask;
    private bool _initializing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmitSetup))]
    [NotifyPropertyChangedFor(nameof(SetupHint))]
    [NotifyCanExecuteChangedFor(nameof(SetupCommand))]
    private VaultProtectorChoice? _selectedProtector;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmitSetup))]
    [NotifyPropertyChangedFor(nameof(SetupHint))]
    [NotifyCanExecuteChangedFor(nameof(SetupCommand))]
    private string _passphrase = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmitSetup))]
    [NotifyPropertyChangedFor(nameof(SetupHint))]
    [NotifyCanExecuteChangedFor(nameof(SetupCommand))]
    private string _confirmPassphrase = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(UnlockCommand))]
    private string _unlockPassphrase = "";

    [ObservableProperty]
    private string? _lastError;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSubmitSetup))]
    [NotifyCanExecuteChangedFor(nameof(SetupCommand))]
    [NotifyCanExecuteChangedFor(nameof(UnlockCommand))]
    private bool _isBusy;

    /// <summary>Creates the view model.</summary>
    /// <param name="vault">The secret vault.</param>
    /// <param name="protectors">The key protectors available on this platform.</param>
    /// <param name="reloader">Refreshes the secret-bearing view models.</param>
    public VaultViewModel(ISecretVault vault, IKeyProtectorFactory protectors, IVaultReloader reloader)
    {
        ArgumentNullException.ThrowIfNull(vault);
        ArgumentNullException.ThrowIfNull(protectors);
        ArgumentNullException.ThrowIfNull(reloader);
        _vault = vault;
        _reloader = reloader;
        State = vault.State;
        Protectors = [.. protectors.Available()
            .Select(p => new VaultProtectorChoice(p.Id, LabelOf(p.Id), p.RequiresPassphrase))
            .OrderBy(c => c.Id == "passphrase")];
        _selectedProtector = Protectors.Count > 0 ? Protectors[0] : null;
        vault.StateChanged += OnStateChanged;
    }

    /// <summary>Raised when the UI should open the vault setup dialog.</summary>
    public event EventHandler? SetupRequested;

    /// <summary>Raised when the UI should open the vault unlock dialog.</summary>
    public event EventHandler? UnlockRequested;

    /// <inheritdoc />
    public event EventHandler? StateChanged;

    /// <summary>Gets the vault state.</summary>
    public VaultState State { get; private set; }

    /// <summary>Gets the status text.</summary>
    public string StatusText => State switch
    {
        VaultState.NotCreated => Strings.VaultStatusNone,
        VaultState.Locked => Strings.VaultStatusLocked,
        _ => Strings.VaultStatusUnlocked,
    };

    /// <summary>Gets the label of the vault protector; <see langword="null"/> when no vault exists.</summary>
    public string? ProtectorLabel => _vault.ProtectorId is { } id ? LabelOf(id) : null;

    /// <summary>Gets a value indicating whether the vault is unlocked.</summary>
    public bool IsUnlocked => State == VaultState.Unlocked;

    /// <summary>Gets a value indicating whether the vault is locked and waits for a passphrase.</summary>
    public bool NeedsPassphrase => State == VaultState.Locked;

    /// <summary>Gets a value indicating whether the vault can be created.</summary>
    public bool CanSetUp => State == VaultState.NotCreated;

    /// <summary>Gets the protectors offered at setup, the platform one first and the passphrase one last.</summary>
    public IReadOnlyList<VaultProtectorChoice> Protectors { get; }

    /// <summary>Gets a value indicating whether the setup form is valid.</summary>
    public bool CanSubmitSetup => !IsBusy && SelectedProtector is { } p
        && (!p.RequiresPassphrase || (Passphrase.Length >= MinPassphraseLength && Passphrase == ConfirmPassphrase));

    /// <summary>Gets the inline validation message of the setup form; empty when there is nothing to report.</summary>
    public string SetupHint => SelectedProtector is { RequiresPassphrase: true } && Passphrase.Length > 0
        ? Passphrase.Length < MinPassphraseLength
            ? Strings.VaultHintMinLength(MinPassphraseLength)
            : ConfirmPassphrase.Length > 0 && Passphrase != ConfirmPassphrase ? Strings.VaultHintMismatch : ""
        : "";

    /// <summary>Reads the vault and raises the first reload.</summary>
    /// <returns>A task that completes when the vault is read and the reload is done.</returns>
    public async Task InitializeAsync()
    {
        _initializing = true;
        try
        {
            await _vault.InitializeAsync(CancellationToken.None);
        }
        catch (VaultException ex)
        {
            LastError = ex.Message;
        }
        finally
        {
            _initializing = false;
        }

        Refresh();
        await _reloader.ReloadAsync();
    }

    [RelayCommand(CanExecute = nameof(CanSubmitSetup))]
    private async Task SetupAsync()
    {
        var protector = SelectedProtector!;
        var passphrase = protector.RequiresPassphrase ? Passphrase : null;
        await RunAsync(async () =>
        {
            await _vault.CreateAsync(protector.Id, passphrase, CancellationToken.None);
        });
        Passphrase = "";
        ConfirmPassphrase = "";
    }

    [RelayCommand(CanExecute = nameof(CanUnlock))]
    private async Task UnlockAsync()
    {
        var passphrase = UnlockPassphrase;
        await RunAsync(async () =>
        {
            if (!await _vault.UnlockAsync(passphrase, CancellationToken.None))
            {
                LastError = Strings.VaultErrorWrongPassphrase;
            }
        });
        UnlockPassphrase = "";
    }

    [RelayCommand]
    private async Task LockAsync()
    {
        _vault.Lock();
        UnlockPassphrase = "";
        await _pendingReload;
    }

    [RelayCommand(CanExecute = nameof(CanSetUp))]
    private void RequestSetup() => SetupRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand(CanExecute = nameof(NeedsPassphrase))]
    private void RequestUnlock() => UnlockRequested?.Invoke(this, EventArgs.Empty);

    System.Windows.Input.ICommand IVaultStateSource.RequestUnlockCommand => RequestUnlockCommand;

    private static string LabelOf(string id) => id switch
    {
        "dpapi" => Strings.VaultProtectorDpapi,
        "keychain" => Strings.VaultProtectorKeychain,
        "secret-service" => Strings.VaultProtectorSecretService,
        "passphrase" => Strings.VaultProtectorPassphrase,
        _ => id,
    };

    private bool CanUnlock() => !IsBusy && UnlockPassphrase.Length > 0;

    private async Task RunAsync(Func<Task> action)
    {
        IsBusy = true;
        LastError = null;
        try
        {
            await action();
        }
        catch (VaultException ex)
        {
            LastError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }

        await _pendingReload;
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (_vault.State == State)
        {
            return;
        }

        Refresh();
        if (!_initializing)
        {
            _pendingReload = _reloader.ReloadAsync();
        }
    }

    private void Refresh()
    {
        State = _vault.State;
        OnPropertyChanged(nameof(State));
        StateChanged?.Invoke(this, EventArgs.Empty);
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ProtectorLabel));
        OnPropertyChanged(nameof(IsUnlocked));
        OnPropertyChanged(nameof(NeedsPassphrase));
        OnPropertyChanged(nameof(CanSetUp));
        RequestSetupCommand.NotifyCanExecuteChanged();
        RequestUnlockCommand.NotifyCanExecuteChanged();
    }
}
