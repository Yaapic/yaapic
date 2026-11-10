using Yaapic.Core.Scripting;

namespace Yaapic.ViewModels.Requests;

/// <summary>Persists the variable changes made by the scripts of a send.</summary>
public interface IScriptVariableApplier
{
    /// <summary>Applies changes to the persisted scopes.</summary>
    /// <param name="requestId">The request that was sent.</param>
    /// <param name="changes">The changes made by its scripts, in order.</param>
    /// <returns>One error message per change that could not be saved; empty when all were.</returns>
    Task<IReadOnlyList<string>> ApplyAsync(Guid requestId, IReadOnlyList<VariableChange> changes);
}
