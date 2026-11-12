using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Yaapic.Core.Variables;
using Yaapic.Localization;
using Yaapic.ViewModels.Editing;

namespace Yaapic.ViewModels.Requests;

/// <summary>A scope a popup can create a variable in.</summary>
/// <param name="Kind">The scope.</param>
/// <param name="Label">The label shown in the picker.</param>
/// <param name="IsEnabled">Whether the scope can be written now.</param>
/// <param name="DisabledReason">Why the scope is disabled; <see langword="null"/> when enabled.</param>
public sealed record VariableScopeChoice(VariableScopeKind Kind, string Label, bool IsEnabled, string? DisabledReason);

/// <summary>Details of a variable under the pointer and the form to create or change it.</summary>
public sealed partial class VariablePopupViewModel : ObservableObject
{
    /// <summary>The message shown for an unresolved variable.</summary>
    public static string UnresolvedMessage => Strings.VariablesUnresolvedMessage;

    /// <summary>The message shown for a dynamic variable.</summary>
    public static string DynamicMessage => Strings.VariablesDynamicMessage;

    /// <summary>The title shown for an unresolved variable.</summary>
    public static string UnresolvedTitle => Strings.VariablesUnresolvedTitle;

    private readonly VariableContext _context;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private VariableScopeKind _selectedScope;

    [ObservableProperty]
    private string _editableValue;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayValue))]
    private bool _isRevealed;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SubmitCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string? _lastError;

    internal VariablePopupViewModel(VariableContext context, VariableHint hint, IReadOnlyList<VariableScopeKind> writable)
    {
        _context = context;
        Name = hint.Name;
        IsResolved = hint.IsResolved;
        IsDynamic = hint.Scope == VariableScopeKind.Dynamic;
        Value = hint.Value;
        IsSecret = hint.IsSecret;
        Scope = hint.Scope;
        ScopeChoices =
        [
            Choice(VariableScopeKind.Environment, Strings.VariablesScopeActiveEnvironment, writable, Strings.VariablesReasonNoEnvironment),
            Choice(VariableScopeKind.Collection, Strings.VariablesScopeCollection, writable, Strings.VariablesReasonNoCollection),
            Choice(VariableScopeKind.Globals, Strings.VariablesScopeGlobals, writable, Strings.VariablesReasonNoGlobals),
        ];
        var first = ScopeChoices.FirstOrDefault(c => c.IsEnabled)?.Kind ?? VariableScopeKind.Globals;
        _selectedScope = hint.Scope is { } own && ScopeChoices.Any(c => c.Kind == own && c.IsEnabled) ? own : first;
        _editableValue = hint.IsSecret ? "" : hint.Value ?? ""; // a secret never lands in the form
        _isEditing = !IsResolved;
    }

    /// <summary>Raised after the variable was written.</summary>
    public event EventHandler? Submitted;

    /// <summary>Gets the variable name.</summary>
    public string Name { get; }

    /// <summary>Gets a value indicating whether the variable is defined in any scope.</summary>
    public bool IsResolved { get; }

    /// <summary>Gets a value indicating whether the variable is a built-in dynamic one.</summary>
    public bool IsDynamic { get; }

    /// <summary>Gets the stored value; <see langword="null"/> when unresolved or dynamic.</summary>
    public string? Value { get; }

    /// <summary>Gets a value indicating whether the value comes from a secret row.</summary>
    public bool IsSecret { get; }

    /// <summary>Gets the value as displayed: masked when secret and not revealed; <see langword="null"/> when there is no value.</summary>
    public string? DisplayValue => IsSecret && !IsRevealed && !string.IsNullOrEmpty(Value) ? KeyValueRowViewModel.Mask : Value;

    /// <summary>Gets the scope the value comes from; <see langword="null"/> when unresolved.</summary>
    public VariableScopeKind? Scope { get; }

    /// <summary>Gets the label of <see cref="Scope"/>; empty when unresolved.</summary>
    public string ScopeLabel => Scope switch
    {
        VariableScopeKind.Environment => Strings.VariablesScopeEnvironment,
        VariableScopeKind.Collection => Strings.VariablesScopeCollection,
        VariableScopeKind.Globals => Strings.VariablesScopeGlobals,
        VariableScopeKind.Dynamic => Strings.VariablesScopeDynamic,
        VariableScopeKind.Local => Strings.VariablesScopeLocal,
        _ => "",
    };

    /// <summary>Gets the popup title.</summary>
    public string Title => IsResolved ? Name : UnresolvedTitle;

    /// <summary>Gets the explanation under the title.</summary>
    public string Message => !IsResolved ? UnresolvedMessage : IsDynamic ? DynamicMessage : "";

    /// <summary>Gets the scopes offered by the form, in the order environment, collection, globals.</summary>
    public IReadOnlyList<VariableScopeChoice> ScopeChoices { get; }

    /// <summary>Gets a value indicating whether the Edit toggle is offered.</summary>
    public bool CanEdit => IsResolved && !IsDynamic && !IsSecret;

    /// <summary>Gets the label of the submit button.</summary>
    public string SubmitLabel => IsResolved ? Strings.CommonSave : Strings.CommonCreate;

    /// <summary>Gets a value indicating whether the form can be submitted.</summary>
    public bool CanSubmit => !IsDynamic && !IsBusy && VariableContext.IsCreatableName(Name)
        && ScopeChoices.Any(c => c.Kind == SelectedScope && c.IsEnabled);

    private static VariableScopeChoice Choice(VariableScopeKind kind, string label, IReadOnlyList<VariableScopeKind> writable, string reason)
    {
        var enabled = writable.Contains(kind);
        return new(kind, label, enabled, enabled ? null : reason);
    }

    [RelayCommand]
    private void Reveal() => IsRevealed = !IsRevealed;

    [RelayCommand]
    private void ToggleEdit()
    {
        if (CanEdit)
        {
            IsEditing = !IsEditing;
        }
    }

    [RelayCommand]
    private void Open() => _context.RaiseOpenRequested(Scope ?? SelectedScope);

    [RelayCommand(CanExecute = nameof(CanSubmit))]
    private async Task SubmitAsync()
    {
        IsBusy = true;
        LastError = null;
        string? error;
        try
        {
            error = await _context.SetAsync(SelectedScope, Name, EditableValue);
        }
        finally
        {
            IsBusy = false;
        }

        if (error is not null)
        {
            LastError = error;
            return;
        }

        if (IsResolved)
        {
            IsEditing = false;
        }

        Submitted?.Invoke(this, EventArgs.Empty);
    }
}
