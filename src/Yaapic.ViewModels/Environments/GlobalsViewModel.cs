using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Storage;
using Yaapic.Domain.Http;
using Yaapic.ViewModels.Editing;

namespace Yaapic.ViewModels.Environments;

/// <summary>Edits the workspace-wide global variables, with dirty tracking against the last saved state.</summary>
public sealed partial class GlobalsViewModel : ObservableObject
{
    private readonly IGlobalsStore _store;
    private IReadOnlyList<KeyValueEntry> _baseline = [];

    [ObservableProperty]
    private string? _lastError;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private bool _canSave = true;

    /// <summary>Creates the view model.</summary>
    /// <param name="store">Where the global variables are saved.</param>
    public GlobalsViewModel(IGlobalsStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        Variables.Changed += (_, _) => OnPropertyChanged(nameof(IsDirty));
    }

    /// <summary>Gets the variables editor.</summary>
    public KeyValueEditorViewModel Variables { get; } = new();

    /// <summary>Gets a value indicating whether the current entries differ from the last saved state.</summary>
    public bool IsDirty => !Variables.ToEntries().SequenceEqual(_baseline);

    /// <summary>Gets the current entries, saved or not; unsaved edits are used when sending.</summary>
    public IReadOnlyList<KeyValueEntry> Current => Variables.ToEntries();

    /// <summary>Loads the variables from the store and records them as the saved state.</summary>
    /// <returns>A task that completes when the variables are loaded.</returns>
    public async Task LoadAsync()
    {
        IReadOnlyList<KeyValueEntry> entries;
        try
        {
            entries = await _store.LoadAsync(CancellationToken.None);
        }
        catch (StorageException ex)
        {
            // A corrupt file must never be overwritten by an empty editor.
            LastError = ex.Message;
            CanSave = false;
            Reset([]);
            return;
        }

        LastError = null;
        CanSave = true;
        Reset(entries);
    }

    private void Reset(IReadOnlyList<KeyValueEntry> entries)
    {
        Variables.Load(entries);
        _baseline = Variables.ToEntries();
        OnPropertyChanged(nameof(IsDirty));
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private async Task SaveAsync()
    {
        var entries = Variables.ToEntries();
        try
        {
            await _store.SaveAsync(entries, CancellationToken.None);
        }
        catch (StorageException ex)
        {
            LastError = ex.Message;
            return;
        }

        LastError = null;
        _baseline = entries;
        OnPropertyChanged(nameof(IsDirty));
    }
}
