using Yaapic.Core.Scripting;
using Yaapic.Core.Variables;

namespace Yaapic.Core.Runner;

/// <summary>The port through which the runner reads the variable scopes of a request and persists the changes of scripts.</summary>
public interface IRunScopeSource
{
    /// <summary>Gets the current persisted scopes a request is resolved against.</summary>
    /// <param name="requestId">The request about to be sent.</param>
    /// <returns>The scopes, without iteration data.</returns>
    VariableScopes GetScopes(Guid requestId);

    /// <summary>Gets the scopes a script may write to for a request.</summary>
    /// <param name="requestId">The request about to be sent.</param>
    /// <returns>The writable scope kinds.</returns>
    IReadOnlyList<VariableScopeKind> GetWritableScopes(Guid requestId);

    /// <summary>Persists variable changes made by scripts.</summary>
    /// <param name="requestId">The request whose scripts made the changes.</param>
    /// <param name="changes">The changes to save.</param>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>One error message per scope that could not be saved; empty when everything was saved.</returns>
    Task<IReadOnlyList<string>> PersistAsync(Guid requestId, IReadOnlyList<VariableChange> changes, CancellationToken ct);
}
