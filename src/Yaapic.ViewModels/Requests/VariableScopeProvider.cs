using Yaapic.Core.Variables;
using Yaapic.ViewModels.Collections;
using Yaapic.ViewModels.Environments;

namespace Yaapic.ViewModels.Requests;

/// <summary>Combines the active environment, the request's collection and the globals.</summary>
public sealed class VariableScopeProvider : IVariableScopeProvider
{
    private readonly EnvironmentsViewModel _environments;
    private readonly GlobalsViewModel _globals;
    private readonly CollectionTreeViewModel _collections;

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
        environments.ActiveEnvironmentChanged += (_, _) => ScopesChanged?.Invoke(this, EventArgs.Empty);
        globals.Variables.Changed += (_, _) => ScopesChanged?.Invoke(this, EventArgs.Empty);
        collections.Refreshed += (_, _) => ScopesChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public event EventHandler? ScopesChanged;

    /// <inheritdoc/>
    public VariableScopes GetScopes(Guid requestId) =>
        new(_environments.Current, _collections.FindCollectionVariables(requestId), _globals.Current);
}
