using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Storage;
using Yaapic.Domain.Collections;
using Yaapic.ViewModels.Editing;

namespace Yaapic.ViewModels.Collections;

/// <summary>Edits the name and description of a folder and, for a collection, its variables.</summary>
public sealed partial class FolderSettingsViewModel : ObservableObject
{
    private readonly ICollectionStore _store;
    private FolderDefinition _baseline;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDirty))]
    private string _name;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDirty))]
    private string _description;

    [ObservableProperty]
    private string? _lastError;

    /// <summary>Creates the view model.</summary>
    /// <param name="folder">The folder to edit; its current state is the saved baseline.</param>
    /// <param name="isCollection">Whether the folder is a top-level one; only those expose variables.</param>
    /// <param name="store">Where the folder is saved.</param>
    public FolderSettingsViewModel(FolderDefinition folder, bool isCollection, ICollectionStore store)
    {
        ArgumentNullException.ThrowIfNull(folder);
        ArgumentNullException.ThrowIfNull(store);
        _store = store;
        _baseline = folder;
        _name = folder.Name;
        _description = folder.Description;
        ShowVariables = isCollection;
        Variables.Load(folder.Variables);
        Variables.Changed += (_, _) => OnPropertyChanged(nameof(IsDirty));
    }

    /// <summary>Raised after the folder has been saved.</summary>
    public event EventHandler? Saved;

    /// <summary>Gets the variables editor.</summary>
    public KeyValueEditorViewModel Variables { get; } = new();

    /// <summary>Gets a value indicating whether the variables are editable (top-level folders only).</summary>
    public bool ShowVariables { get; }

    /// <summary>Gets a value indicating whether the editor differs from the last saved state.</summary>
    public bool IsDirty => Name != _baseline.Name || Description != _baseline.Description
        || (ShowVariables && !Variables.ToEntries().SequenceEqual(_baseline.Variables));

    [RelayCommand]
    private async Task SaveAsync()
    {
        var saved = _baseline with
        {
            Name = Name.Trim() is { Length: > 0 } name ? name : _baseline.Name,
            Description = Description,
            Variables = ShowVariables ? Variables.ToEntries() : _baseline.Variables,
        };
        try
        {
            await _store.SaveFolderAsync(saved, null, CancellationToken.None);
        }
        catch (StorageException ex)
        {
            LastError = ex.Message;
            return;
        }

        LastError = null;
        _baseline = saved;
        Name = saved.Name;
        OnPropertyChanged(nameof(IsDirty));
        Saved?.Invoke(this, EventArgs.Empty);
    }
}
