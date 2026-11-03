using Yaapic.Core.Variables;

namespace Yaapic.ViewModels.Requests;

/// <summary>Exposes the variable scopes applied when a request is sent.</summary>
public interface IVariableScopeProvider
{
    /// <summary>Occurs when the active environment, the global variables or the collection tree changed.</summary>
    event EventHandler? ScopesChanged;

    /// <summary>Gets the scopes to resolve a request against.</summary>
    /// <param name="requestId">The identifier of the request being sent.</param>
    /// <returns>The environment, collection and global variables.</returns>
    VariableScopes GetScopes(Guid requestId);
}
