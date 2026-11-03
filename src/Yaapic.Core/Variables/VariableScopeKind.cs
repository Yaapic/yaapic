namespace Yaapic.Core.Variables;

/// <summary>The scope a variable was resolved from, in decreasing precedence (ADR 0008).</summary>
public enum VariableScopeKind
{
    /// <summary>The active environment.</summary>
    Environment,

    /// <summary>The variables of the collection (or folder) the request belongs to.</summary>
    Collection,

    /// <summary>The workspace-wide global variables.</summary>
    Globals,

    /// <summary>The built-in dynamic variables such as <c>$guid</c>.</summary>
    Dynamic,
}
