using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Secrets;
using Yaapic.Domain.Http;

namespace Yaapic.ViewModels.Editing;

/// <summary>Key/value table editor (params, headers, variables) that always ends with one empty placeholder row.</summary>
public sealed partial class KeyValueEditorViewModel : ObservableObject
{
    private bool _loading;

    /// <summary>Initializes the editor with a single placeholder row.</summary>
    public KeyValueEditorViewModel() => AddRow();

    /// <summary>Raised when a row's key, value or enabled flag changes, or a row is removed; never raised by <see cref="Load"/>.</summary>
    public event EventHandler? Changed;

    /// <summary>Gets or sets a value indicating whether rows can be secret; only the variable editors turn it on, so a flag never leaks into params, headers or forms.</summary>
    public bool AllowSecrets { get; set; }

    /// <summary>Gets a value indicating whether the vault is unlocked, so rows can be marked secret and secret values edited.</summary>
    public bool SecretsAvailable { get; private set; }

    /// <summary>Gets the rows; the last one is always the empty placeholder.</summary>
    public ObservableCollection<KeyValueRowViewModel> Rows { get; } = [];

    /// <summary>Gets the number of rows that are enabled and not the empty placeholder.</summary>
    public int ActiveCount => Rows.Count(r => r.Enabled && !r.IsEmpty);

    /// <summary>Replaces all rows with <paramref name="entries"/> followed by a placeholder, without raising <see cref="Changed"/>.</summary>
    /// <param name="entries">The entries to show, in order.</param>
    public void Load(IEnumerable<KeyValueEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _loading = true;
        try
        {
            foreach (var row in Rows)
            {
                row.PropertyChanged -= OnRowPropertyChanged;
            }

            Rows.Clear();
            foreach (var entry in entries)
            {
                AddRow(entry);
            }

            AddRow();
        }
        finally
        {
            _loading = false;
            OnPropertyChanged(nameof(ActiveCount));
        }
    }

    /// <summary>Tells the rows whether the vault is unlocked; locking hides every revealed value.</summary>
    /// <param name="state">The vault state.</param>
    public void SetVaultState(VaultState state)
    {
        SecretsAvailable = state == VaultState.Unlocked;
        OnPropertyChanged(nameof(SecretsAvailable));
        foreach (var row in Rows)
        {
            row.SecretsUnlocked = SecretsAvailable;
        }
    }

    /// <summary>Returns the non-empty rows as entries, in order.</summary>
    /// <returns>The entries.</returns>
    public IReadOnlyList<KeyValueEntry> ToEntries() =>
        [.. Rows.Where(r => !r.IsEmpty).Select(r => new KeyValueEntry(r.Key, r.Value, r.Enabled, AllowSecrets && r.IsSecret))];

    /// <summary>Removes a row; the placeholder row cannot be removed.</summary>
    /// <param name="row">The row to remove.</param>
    [RelayCommand]
    private void RemoveRow(KeyValueRowViewModel row)
    {
        if (row == Rows[^1] || !Rows.Remove(row))
        {
            return;
        }

        row.PropertyChanged -= OnRowPropertyChanged;
        OnPropertyChanged(nameof(ActiveCount));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void AddRow(KeyValueEntry? entry = null)
    {
        var row = new KeyValueRowViewModel();
        if (entry is not null)
        {
            row.Key = entry.Key;
            row.Value = entry.Value;
            row.Enabled = entry.Enabled;
            row.IsSecret = AllowSecrets && entry.IsSecret;
        }

        row.SecretsUnlocked = SecretsAvailable;
        row.PropertyChanged += OnRowPropertyChanged;
        Rows.Add(row);
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_loading || e.PropertyName is not (nameof(KeyValueRowViewModel.Key) or nameof(KeyValueRowViewModel.Value)
            or nameof(KeyValueRowViewModel.Enabled) or nameof(KeyValueRowViewModel.IsSecret)))
        {
            return;
        }

        if (sender == Rows[^1] && !((KeyValueRowViewModel)sender).IsEmpty)
        {
            AddRow();
        }

        OnPropertyChanged(nameof(ActiveCount));
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
