using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Localization;

namespace Yaapic.ViewModels.Editing;

/// <summary>One editable row (key, value, enabled flag, secret flag) of a <see cref="KeyValueEditorViewModel"/>.</summary>
public sealed partial class KeyValueRowViewModel : ObservableObject
{
    /// <summary>The text shown instead of a secret value.</summary>
    public const string Mask = "••••••••";

    /// <summary>The error shown when a row is marked secret while the vault is not unlocked.</summary>
    public static string VaultLockedError => Strings.EditingVaultLockedError;

    /// <summary>The hint shown once a row stops being secret.</summary>
    public static string StoredInWorkspaceHint => Strings.EditingStoredInWorkspaceHint;

    private bool _secretsUnlocked;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private string _key = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(DisplayValue))]
    [NotifyPropertyChangedFor(nameof(IsMasked))]
    private string _value = "";

    [ObservableProperty]
    private bool _enabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayValue))]
    [NotifyPropertyChangedFor(nameof(IsMasked))]
    [NotifyPropertyChangedFor(nameof(CanEditValue))]
    [NotifyPropertyChangedFor(nameof(CanToggleSecret))]
    [NotifyPropertyChangedFor(nameof(SecretToggleTooltip))]
    private bool _isSecret;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayValue))]
    [NotifyPropertyChangedFor(nameof(IsMasked))]
    private bool _isRevealed;

    [ObservableProperty]
    private string? _lastError;

    [ObservableProperty]
    private string? _hint;

    /// <summary>Gets a value indicating whether both key and value are empty.</summary>
    public bool IsEmpty => Key.Length == 0 && Value.Length == 0;

    /// <summary>Gets a value indicating whether the mask replaces the value: a secret that is not revealed and either has a value or cannot be read because the vault is locked.</summary>
    public bool IsMasked => IsSecret && !IsRevealed && (Value.Length > 0 || !_secretsUnlocked);

    /// <summary>Gets the value as displayed: <see cref="Mask"/> when <see cref="IsMasked"/>.</summary>
    public string DisplayValue => IsMasked ? Mask : Value;

    /// <summary>Gets a value indicating whether the value can be edited; secret rows are read-only while the vault is locked.</summary>
    public bool CanEditValue => !IsSecret || _secretsUnlocked;

    /// <summary>Gets a value indicating whether the secret flag can be toggled: a secret row can always be unflagged, a plain one only while the vault is unlocked.</summary>
    public bool CanToggleSecret => IsSecret || _secretsUnlocked;

    /// <summary>Gets the tooltip of the secret toggle.</summary>
    public string SecretToggleTooltip => CanToggleSecret ? Strings.EditingSecretToggleStore : Strings.EditingSecretToggleLocked;

    internal bool SecretsUnlocked
    {
        get => _secretsUnlocked;
        set
        {
            if (_secretsUnlocked == value)
            {
                return;
            }

            _secretsUnlocked = value;
            if (!value)
            {
                IsRevealed = false;
            }

            OnPropertyChanged(nameof(CanEditValue));
            OnPropertyChanged(nameof(IsMasked));
            OnPropertyChanged(nameof(DisplayValue));
            OnPropertyChanged(nameof(CanToggleSecret));
            OnPropertyChanged(nameof(SecretToggleTooltip));
        }
    }

    [RelayCommand]
    private void ToggleReveal() => IsRevealed = !IsRevealed;

    [RelayCommand]
    private void ToggleSecret()
    {
        LastError = null;
        Hint = null;
        if (IsSecret)
        {
            IsSecret = false;
            IsRevealed = false;
            Hint = StoredInWorkspaceHint;
        }
        else if (_secretsUnlocked)
        {
            IsSecret = true;
        }
        else
        {
            LastError = VaultLockedError;
        }
    }
}
