using Yaapic.Core.Runner;
using Yaapic.Core.Scripting;
using Yaapic.Core.Variables;
using Yaapic.ViewModels.Requests;

namespace Yaapic.ViewModels.Runner;

/// <summary>Feeds the runner with the scopes of the app and saves the variable changes of scripts through them.</summary>
/// <param name="provider">The variable scope provider of the app.</param>
public sealed class VariableScopeRunSource(IVariableScopeProvider provider) : IRunScopeSource
{
    /// <inheritdoc/>
    public VariableScopes GetScopes(Guid requestId) => provider.GetScopes(requestId);

    /// <inheritdoc/>
    public IReadOnlyList<VariableScopeKind> GetWritableScopes(Guid requestId) => provider.GetWritableScopes(requestId);

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> PersistAsync(Guid requestId, IReadOnlyList<VariableChange> changes, CancellationToken ct) =>
        provider.ApplyChangesAsync(requestId, changes);
}
