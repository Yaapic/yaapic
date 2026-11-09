using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Yaapic.ViewModels.Editing;

/// <summary>One editable row (key, value, enabled flag, secret flag) of a <see cref="KeyValueEditorViewModel"/>.</summary>
public sealed partial class KeyValueRowViewModel : ObservableObject
{
    /// <summary>The text shown instead of a secret value.</summary>
    public const string Mask = "••••••••";

    /// <summary>The error shown when a row is marked secret while the vault is not unlocked.</summary>
    public const string VaultLockedError = "Unlock the vault to mark a variable as secret";

    /// <summary>The hint shown once a row stops being secret.</summary>
    public const string StoredInWorkspaceHint = "This value will be stored in the workspace file.";

    private bool _secretsUnlocked;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private string _key = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    [NotifyPropertyChangedFor(nameof(DisplayValue))]
    private string _value = "";

    [ObservableProperty]
    private bool _enabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayValue))]
    [NotifyPropertyChangedFor(nameof(CanEditValue))]
    private bool _isSecret;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayValue))]
    private bool _isRevealed;

    [ObservableProperty]
    private string? _lastError;

    [ObservableProperty]
    private string? _hint;

    /// <summary>Gets a value indicating whether both key and value are empty.</summary>
    public bool IsEmpty => Key.Length == 0 && Value.Length == 0;

    /// <summary>Gets the value as displayed: masked when secret, not revealed and not empty.</summary>
    public string DisplayValue => IsSecret && !IsRevealed && Value.Length > 0 ? Mask : Value;

    /// <summary>Gets a value indicating whether the value can be edited; secret rows are read-only while the vault is locked.</summary>
    public bool CanEditValue => !IsSecret || _secretsUnlocked;

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
