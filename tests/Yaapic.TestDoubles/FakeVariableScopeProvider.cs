using Yaapic.Core.Scripting;
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

    /// <summary>Gets or sets the scopes reported as writable.</summary>
    public IReadOnlyList<VariableScopeKind> WritableScopes { get; set; } = [VariableScopeKind.Environment, VariableScopeKind.Collection, VariableScopeKind.Globals];

    /// <summary>Gets the writes received, in order.</summary>
    public List<(VariableScopeKind Scope, string Name, string Value)> Writes { get; } = [];

    /// <summary>Gets or sets the error returned by <see cref="SetVariableAsync"/>; <see langword="null"/> succeeds.</summary>
    public string? WriteError { get; set; }

    /// <inheritdoc/>
    public IReadOnlyList<VariableScopeKind> GetWritableScopes(Guid requestId) => WritableScopes;

    /// <inheritdoc/>
    public Task<string?> SetVariableAsync(Guid requestId, VariableScopeKind scope, string name, string value)
    {
        Writes.Add((scope, name, value));
        return Task.FromResult(WriteError);
    }

    /// <summary>Gets the unsets received, in order.</summary>
    public List<(VariableScopeKind Scope, string Name)> Unsets { get; } = [];

    /// <summary>Gets the batches passed to <see cref="ApplyChangesAsync"/>, in order.</summary>
    public List<IReadOnlyList<VariableChange>> AppliedBatches { get; } = [];

    /// <inheritdoc/>
    public Task<string?> UnsetVariableAsync(Guid requestId, VariableScopeKind scope, string name)
    {
        Unsets.Add((scope, name));
        return Task.FromResult(WriteError);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<string>> ApplyChangesAsync(Guid requestId, IReadOnlyList<VariableChange> changes)
    {
        AppliedBatches.Add(changes);
        return Task.FromResult<IReadOnlyList<string>>(WriteError is null ? [] : [WriteError]);
    }

    /// <summary>Raises <see cref="ScopesChanged"/>.</summary>
    public void RaiseScopesChanged() => ScopesChanged?.Invoke(this, EventArgs.Empty);
}
