using Yaapic.Core.Variables;

namespace Yaapic.ViewModels.Requests;

/// <summary>How a variable token resolves.</summary>
public enum VariableTokenState
{
    /// <summary>Defined in the environment, the collection or the globals.</summary>
    Resolved,

    /// <summary>A built-in dynamic variable such as <c>$guid</c>.</summary>
    Dynamic,

    /// <summary>Defined nowhere.</summary>
    Unresolved,
}

/// <summary>A <c>{{variable}}</c> located in a text, with its resolution.</summary>
/// <param name="Start">The index of the first brace.</param>
/// <param name="Length">The length of the whole token, braces and inner spaces included.</param>
/// <param name="Hint">How the variable resolves.</param>
public sealed record VariableToken(int Start, int Length, VariableHint Hint)
{
    /// <summary>Gets the variable name.</summary>
    public string Name => Hint.Name;

    /// <summary>Gets the index just after the closing braces.</summary>
    public int End => Start + Length;

    /// <summary>Gets the resolution state.</summary>
    public VariableTokenState State => !Hint.IsResolved
        ? VariableTokenState.Unresolved
        : Hint.Scope == VariableScopeKind.Dynamic ? VariableTokenState.Dynamic : VariableTokenState.Resolved;
}
