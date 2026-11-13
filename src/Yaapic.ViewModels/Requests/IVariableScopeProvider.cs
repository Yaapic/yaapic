using Yaapic.Core.Scripting;
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

    /// <summary>Gets the environments offered for a request other than the active one, so their variables can be suggested.</summary>
    /// <param name="requestId">The request being edited.</param>
    /// <returns>The inactive environments available for the request's collection; none by default.</returns>
    IReadOnlyList<Yaapic.Domain.Environments.EnvironmentDefinition> GetOtherEnvironments(Guid requestId) => [];

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

    /// <summary>Removes the variable of that exact name from a scope; nothing happens when it is absent.</summary>
    /// <param name="requestId">The request the variable belongs to.</param>
    /// <param name="scope">The scope to write; it must be one of <see cref="GetWritableScopes"/>.</param>
    /// <param name="name">The variable name.</param>
    /// <returns>An error message, or <see langword="null"/> when the scope was saved.</returns>
    Task<string?> UnsetVariableAsync(Guid requestId, VariableScopeKind scope, string name);

    /// <summary>Applies the variable changes made by scripts, saving each scope once and raising <see cref="ScopesChanged"/> once.</summary>
    /// <param name="requestId">The request that was sent.</param>
    /// <param name="changes">The changes, in order; changes to a scope that is not writable are skipped.</param>
    /// <returns>One error message per scope that could not be saved; empty when all were.</returns>
    Task<IReadOnlyList<string>> ApplyChangesAsync(Guid requestId, IReadOnlyList<VariableChange> changes);
}
