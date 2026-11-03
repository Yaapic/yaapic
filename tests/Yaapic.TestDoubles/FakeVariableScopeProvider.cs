using Yaapic.Core.Variables;
using Yaapic.ViewModels.Requests;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="IVariableScopeProvider"/> returning fixed scopes.</summary>
public sealed class FakeVariableScopeProvider : IVariableScopeProvider
{
    /// <summary>Gets or sets the scopes returned for every request.</summary>
    public VariableScopes Scopes { get; set; } = VariableScopes.None;

    /// <inheritdoc/>
    public event EventHandler? ScopesChanged;

    /// <inheritdoc/>
    public VariableScopes GetScopes(Guid requestId) => Scopes;

    /// <summary>Raises <see cref="ScopesChanged"/>.</summary>
    public void RaiseScopesChanged() => ScopesChanged?.Invoke(this, EventArgs.Empty);
}
