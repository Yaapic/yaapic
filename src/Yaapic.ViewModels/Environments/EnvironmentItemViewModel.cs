using CommunityToolkit.Mvvm.ComponentModel;
using Yaapic.Domain.Environments;
using Yaapic.ViewModels.Editing;

namespace Yaapic.ViewModels.Environments;

/// <summary>An editable environment: its name and variables, with dirty tracking against the last saved state.</summary>
public sealed partial class EnvironmentItemViewModel : ObservableObject
{
    private EnvironmentDefinition? _baseline;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDirty))]
    private string _name;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isEdited;

    private EnvironmentItemViewModel(EnvironmentDefinition definition, bool isSaved)
    {
        Id = definition.Id;
        _name = definition.Name;
        Variables.Load(definition.Variables);
        Variables.Changed += (_, _) => OnPropertyChanged(nameof(IsDirty));
        _baseline = isSaved ? definition : null;
    }

    /// <summary>Gets the stable identifier.</summary>
    public Guid Id { get; }

    // IsActive: the environment used to resolve requests. IsEdited: the one shown in the editor. Both are set by the list.

    /// <summary>Gets the variables editor.</summary>
    public KeyValueEditorViewModel Variables { get; } = new();

    /// <summary>Gets a value indicating whether the environment was never saved or differs from its last saved state.</summary>
    public bool IsDirty => _baseline is null || !ToDefinition().IsEquivalentTo(_baseline);

    /// <summary>Creates an item for an environment that is already stored.</summary>
    /// <param name="definition">The stored environment.</param>
    /// <returns>A clean item.</returns>
    public static EnvironmentItemViewModel FromSaved(EnvironmentDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new(definition, isSaved: true);
    }

    /// <summary>Creates an item for an environment that was never saved.</summary>
    /// <param name="definition">The new environment.</param>
    /// <returns>A dirty item.</returns>
    public static EnvironmentItemViewModel FromNew(EnvironmentDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return new(definition, isSaved: false);
    }

    /// <summary>Builds the environment from the current edited values.</summary>
    /// <returns>The definition, saved or not.</returns>
    public EnvironmentDefinition ToDefinition() => new(Id, Name, Variables.ToEntries());

    /// <summary>Records the current state as saved.</summary>
    public void MarkSaved()
    {
        _baseline = ToDefinition();
        OnPropertyChanged(nameof(IsDirty));
    }
}
