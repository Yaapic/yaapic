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

    /// <summary>Gets the scopes a variable can be written to for a request.</summary>
    /// <param name="requestId">The request the variable is created for.</param>
    /// <returns>Environment when one is active, Collection when the request is in a collection, and Globals.</returns>
    IReadOnlyList<VariableScopeKind> GetWritableScopes(Guid requestId);

    /// <summary>Creates a variable in a scope, or updates (and enables) the row that already has that name.</summary>
    /// <param name="requestId">The request the variable is created for.</param>
    /// <param name="scope">The scope to write; it must be one of <see cref="GetWritableScopes"/>.</param>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The value.</param>
    /// <returns>An error message, or <see langword="null"/> when the variable was saved.</returns>
    Task<string?> SetVariableAsync(Guid requestId, VariableScopeKind scope, string name, string value);
}
