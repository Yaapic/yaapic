using Yaapic.Core.Variables;

namespace Yaapic.ViewModels.Requests;

/// <summary>A variable used by a request field and how it resolves.</summary>
/// <param name="Name">The variable name.</param>
/// <param name="IsResolved">Whether the variable is defined in any scope.</param>
/// <param name="Value">The stored value; <see langword="null"/> when unresolved or dynamic (it changes per send).</param>
/// <param name="Scope">The scope the value comes from; <see langword="null"/> when unresolved.</param>
/// <param name="IsSecret">Whether the value comes from a secret row; views must mask it.</param>
public sealed record VariableHint(string Name, bool IsResolved, string? Value, VariableScopeKind? Scope, bool IsSecret = false);
