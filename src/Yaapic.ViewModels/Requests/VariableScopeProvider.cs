using Yaapic.Core.Variables;
using Yaapic.ViewModels.Collections;
using Yaapic.ViewModels.Editing;
using Yaapic.ViewModels.Environments;

namespace Yaapic.ViewModels.Requests;

/// <summary>Combines the active environment, the request's collection and the globals.</summary>
public sealed class VariableScopeProvider : IVariableScopeProvider
{
    private readonly EnvironmentsViewModel _environments;
    private readonly GlobalsViewModel _globals;
    private readonly CollectionTreeViewModel _collections;
    private EnvironmentItemViewModel? _watched;

    /// <summary>Creates the provider.</summary>
    /// <param name="environments">Provides the active environment.</param>
    /// <param name="globals">Provides the global variables, including unsaved edits.</param>
    /// <param name="collections">Provides the variables of the collection that contains a request.</param>
    public VariableScopeProvider(EnvironmentsViewModel environments, GlobalsViewModel globals, CollectionTreeViewModel collections)
    {
        ArgumentNullException.ThrowIfNull(environments);
        ArgumentNullException.ThrowIfNull(globals);
        ArgumentNullException.ThrowIfNull(collections);
        _environments = environments;
        _globals = globals;
        _collections = collections;
        Watch(environments.ActiveEnvironment);
        environments.ActiveEnvironmentChanged += (_, _) =>
        {
            Watch(_environments.ActiveEnvironment);
            Raise();
        };
        globals.Variables.Changed += (_, _) => Raise();
        collections.Refreshed += (_, _) => Raise();
    }

    /// <inheritdoc/>
    public event EventHandler? ScopesChanged;

    /// <inheritdoc/>
    public VariableScopes GetScopes(Guid requestId) =>
        new(_environments.Current, _collections.FindCollectionVariables(requestId), _globals.Current);

    /// <inheritdoc/>
    public IReadOnlyList<VariableScopeKind> GetWritableScopes(Guid requestId)
    {
        List<VariableScopeKind> scopes = [];
        if (_environments.ActiveEnvironment is not null)
        {
            scopes.Add(VariableScopeKind.Environment);
        }

        if (_collections.FindCollection(requestId) is not null)
        {
            scopes.Add(VariableScopeKind.Collection);
        }

        scopes.Add(VariableScopeKind.Globals);
        return scopes;
    }

    /// <inheritdoc/>
    public async Task<string?> SetVariableAsync(Guid requestId, VariableScopeKind scope, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        name = name.Trim();
        if (!VariableContext.IsCreatableName(name))
        {
            return "The variable name is not valid.";
        }

        if (!GetWritableScopes(requestId).Contains(scope))
        {
            return "This scope is not available for the request.";
        }

        switch (scope)
        {
            case VariableScopeKind.Environment:
                var item = _environments.ActiveEnvironment!;
                Upsert(item.Variables, name, value);
                await _environments.SaveCommand.ExecuteAsync(item);
                return _environments.LastError;
            case VariableScopeKind.Globals:
                if (!_globals.CanSave)
                {
                    return _globals.LastError ?? "The global variables cannot be saved.";
                }

                Upsert(_globals.Variables, name, value);
                await _globals.SaveCommand.ExecuteAsync(null);
                return _globals.LastError;
            default:
                return await SetCollectionVariableAsync(requestId, name, value);
        }
    }

    // The collection is written through a FolderSettingsViewModel (as the settings dialog does), then the tree is
    // reloaded so ScopesChanged fires with the saved value.
    private async Task<string?> SetCollectionVariableAsync(Guid requestId, string name, string value)
    {
        var folder = _collections.FindCollection(requestId)!;
        var editor = new FolderSettingsViewModel(folder, isCollection: true, _collections.Store);
        Upsert(editor.Variables, name, value);
        await editor.SaveCommand.ExecuteAsync(null);
        if (editor.LastError is { } error)
        {
            return error;
        }

        await _collections.RefreshAsync();
        return null;
    }

    // An existing row (enabled or not) takes the value and is enabled; otherwise the placeholder row becomes the new one.
    private static void Upsert(KeyValueEditorViewModel editor, string name, string value)
    {
        var row = editor.Rows.FirstOrDefault(r => !r.IsEmpty && r.Key == name) ?? editor.Rows[^1];
        row.Key = name;
        row.Value = value;
        row.Enabled = true;
    }

    private void Watch(EnvironmentItemViewModel? item)
    {
        _watched?.Variables.Changed -= OnEnvironmentVariablesChanged;
        _watched = item;
        _watched?.Variables.Changed += OnEnvironmentVariablesChanged;
    }

    private void OnEnvironmentVariablesChanged(object? sender, EventArgs e) => Raise();

    private void Raise() => ScopesChanged?.Invoke(this, EventArgs.Empty);
}
