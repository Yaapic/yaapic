namespace Yaapic.Core.Variables;

/// <summary>A <c>{{name}}</c> placeholder located in a text.</summary>
/// <param name="Start">The index of the first brace.</param>
/// <param name="Length">The length of the whole token, braces and inner spaces included.</param>
/// <param name="Name">The trimmed variable name.</param>
public readonly record struct VariableSpan(int Start, int Length, string Name)
{
    /// <summary>Gets the index just after the closing braces.</summary>
    public int End => Start + Length;
}
