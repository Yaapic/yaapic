using Yaapic.Core.Variables;

namespace Yaapic.Core.Scripting;

/// <summary>A change a script made to a persisted variable scope.</summary>
/// <param name="Scope">The scope that changed.</param>
/// <param name="Name">The variable name.</param>
/// <param name="Value">The new value, or <see langword="null"/> when the variable was unset.</param>
public sealed record VariableChange(VariableScopeKind Scope, string Name, string? Value)
{
    /// <summary>Gets a value indicating whether the variable was removed.</summary>
    public bool IsUnset => Value is null;
}
