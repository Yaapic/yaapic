using Yaapic.Core.Variables;

namespace Yaapic.ViewModels.Requests;

/// <summary>One variable offered while the user types <c>{{</c>.</summary>
/// <param name="Name">The variable name, as written between the braces.</param>
/// <param name="Scope">The scope the name resolves from; for a variable of an inactive environment, <see cref="VariableScopeKind.Environment"/>.</param>
/// <param name="ScopeLabel">Where the variable comes from, for the user: a scope name or an environment name.</param>
/// <param name="Preview">A short value to recognise it by; empty for secrets and dynamic variables.</param>
/// <param name="IsSecret">Whether the row is a secret variable.</param>
/// <param name="IsActive">Whether the name resolves right now; <see langword="false"/> for a variable of an environment that is not active.</param>
public sealed record VariableSuggestion(string Name, VariableScopeKind Scope, string ScopeLabel, string Preview, bool IsSecret, bool IsActive);
