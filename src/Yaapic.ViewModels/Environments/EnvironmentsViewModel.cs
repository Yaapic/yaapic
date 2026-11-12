using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Storage;
using Yaapic.Domain.Environments;
using Yaapic.Localization;
using Yaapic.UI.Abstractions;
using Yaapic.ViewModels.Requests;
using Yaapic.ViewModels.Vault;

namespace Yaapic.ViewModels.Environments;

/// <summary>Selects the active environment and edits environments and their variables.</summary>
public sealed partial class EnvironmentsViewModel : ObservableObject, IActiveEnvironmentProvider
{
    private readonly IEnvironmentStore _store;
    private readonly IDialogService _dialogs;
    private readonly IVaultStateSource? _vaultState;

    [ObservableProperty]
    private EnvironmentItemViewModel? _activeEnvironment;

    [ObservableProperty]
    private EnvironmentItemViewModel? _editedEnvironment;

    [ObservableProperty]
    private string? _lastError;

    [ObservableProperty]
    private bool _isEditingGlobals;

    /// <summary>Creates the view model.</summary>
    /// <param name="store">Where environments are saved.</param>
    /// <param name="dialogs">Asks the user to confirm deletions.</param>
    /// <param name="vaultState">Tells whether secret rows can be edited; <see langword="null"/> means they never can.</param>
    public EnvironmentsViewModel(IEnvironmentStore store, IDialogService dialogs, IVaultStateSource? vaultState = null)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(dialogs);
        _store = store;
        _dialogs = dialogs;
        _vaultState = vaultState;
        if (vaultState is not null)
        {
            vaultState.StateChanged += (_, _) =>
            {
                foreach (var item in Environments)
                {
                    item.Variables.SetVaultState(vaultState.State);
                }
            };
        }
    }

    /// <summary>Raised when <see cref="ActiveEnvironment"/> changes.</summary>
    public event EventHandler? ActiveEnvironmentChanged;

    /// <summary>Gets the known environments.</summary>
    public ObservableCollection<EnvironmentItemViewModel> Environments { get; } = [];

    /// <summary>Gets the active environment with its current edited values, saved or not; null when none is active.</summary>
    public EnvironmentDefinition? Current => ActiveEnvironment?.ToDefinition();

    /// <summary>Loads the environments from the store and selects the one with the given identifier.</summary>
    /// <param name="activeId">The identifier of the environment to activate; null or unknown means none.</param>
    /// <returns>A task that completes when the list is loaded.</returns>
    public async Task LoadAsync(Guid? activeId)
    {
        IReadOnlyList<EnvironmentDefinition> all;
        try
        {
            all = await _store.ListAsync(CancellationToken.None);
            LastError = null;
        }
        catch (StorageException ex)
        {
            LastError = ex.Message;
            return;
        }

        Environments.Clear();
        foreach (var definition in all)
        {
            AddItem(EnvironmentItemViewModel.FromSaved(definition));
        }

        EditedEnvironment = null;
        ActiveEnvironment = activeId is { } id ? Environments.FirstOrDefault(e => e.Id == id) : null;
    }

    /// <summary>Deletes the environment without prompting, for views that confirm inline.</summary>
    /// <param name="item">The environment to delete.</param>
    /// <returns>A task that completes when the environment is removed.</returns>
    public async Task DeleteConfirmedAsync(EnvironmentItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        try
        {
            await _store.DeleteAsync(item.Id, CancellationToken.None);
        }
        catch (StorageException ex)
        {
            LastError = ex.Message;
            return;
        }

        LastError = null;
        Environments.Remove(item);
        if (ActiveEnvironment == item)
        {
            ActiveEnvironment = null;
        }

        if (EditedEnvironment == item)
        {
            EditedEnvironment = null;
        }
    }

    partial void OnActiveEnvironmentChanged(EnvironmentItemViewModel? oldValue, EnvironmentItemViewModel? newValue)
    {
        oldValue?.IsActive = false;
        newValue?.IsActive = true;
        OnPropertyChanged(nameof(Current));
        ActiveEnvironmentChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnEditedEnvironmentChanged(EnvironmentItemViewModel? oldValue, EnvironmentItemViewModel? newValue)
    {
        oldValue?.IsEdited = false;
        newValue?.IsEdited = true;
        if (newValue is not null)
        {
            IsEditingGlobals = false;
        }
    }

    partial void OnIsEditingGlobalsChanged(bool value)
    {
        if (value)
        {
            EditedEnvironment = null;
        }
    }

    [RelayCommand]
    private void Duplicate(EnvironmentItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        var source = item.ToDefinition();
        var copy = EnvironmentItemViewModel.FromNew(new(Guid.NewGuid(), Strings.CommonCopyName(source.Name), source.Variables));
        AddItem(copy);
        EditedEnvironment = copy;
    }

    [RelayCommand]
    private void Add()
    {
        var item = EnvironmentItemViewModel.FromNew(EnvironmentDefinition.Create(Strings.EnvironmentsNewName));
        AddItem(item);
        EditedEnvironment = item;
    }

    private void AddItem(EnvironmentItemViewModel item)
    {
        if (_vaultState is not null)
        {
            item.Variables.UnlockCommand = _vaultState.RequestUnlockCommand;
            item.Variables.SetVaultState(_vaultState.State);
        }

        Environments.Add(item);
    }

    [RelayCommand]
    private async Task SaveAsync(EnvironmentItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        try
        {
            await _store.SaveAsync(item.ToDefinition(), CancellationToken.None);
        }
        catch (StorageException ex)
        {
            LastError = ex.Message;
            return;
        }

        LastError = null;
        item.MarkSaved();
    }

    [RelayCommand]
    private async Task DeleteAsync(EnvironmentItemViewModel item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (await _dialogs.ConfirmDeleteAsync(item.Name))
        {
            await DeleteConfirmedAsync(item);
        }
    }
}
