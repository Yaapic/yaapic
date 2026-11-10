using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Variables;

/// <summary>The variable scopes a request is resolved against: environment, collection, globals, then dynamic.</summary>
/// <param name="Environment">The active environment, or <see langword="null"/> for none.</param>
/// <param name="Collection">The collection variables.</param>
/// <param name="Globals">The global variables.</param>
/// <param name="Local">The variables local to one send; <see langword="null"/> for none. Consulted first.</param>
public sealed record VariableScopes(
    EnvironmentDefinition? Environment,
    IReadOnlyList<KeyValueEntry> Collection,
    IReadOnlyList<KeyValueEntry> Globals,
    IReadOnlyList<KeyValueEntry>? Local = null)
{
    /// <summary>Gets scopes that define nothing besides the dynamic variables.</summary>
    public static VariableScopes None { get; } = new(null, [], []);

    /// <summary>Creates a source that asks the environment, the collection, the globals, then the dynamic variables.</summary>
    /// <returns>The composite source.</returns>
    public IVariableSource CreateSource() =>
        new CompositeVariableSource(
            ListSource(Local ?? []),
            new EnvironmentVariableSource(Environment),
            ListSource(Collection),
            ListSource(Globals),
            new DynamicVariableSource());

    /// <summary>Resolves a variable and reports the scope it came from.</summary>
    /// <param name="name">The exact, case-sensitive variable name.</param>
    /// <param name="value">The value when found; otherwise an empty string.</param>
    /// <param name="scope">The scope of the value when found; otherwise <see cref="VariableScopeKind.Dynamic"/>.</param>
    /// <returns><see langword="true"/> when the variable is defined in any scope.</returns>
    public bool TryResolve(string name, out string value, out VariableScopeKind scope) =>
        TryResolve(name, out value, out scope, out _);

    /// <summary>Resolves a variable and reports the scope it came from and whether its row is secret.</summary>
    /// <param name="name">The exact, case-sensitive variable name.</param>
    /// <param name="value">The value when found; otherwise an empty string.</param>
    /// <param name="scope">The scope of the value when found; otherwise <see cref="VariableScopeKind.Dynamic"/>.</param>
    /// <param name="isSecret">Whether the winning row is a secret variable.</param>
    /// <returns><see langword="true"/> when the variable is defined in any scope.</returns>
    public bool TryResolve(string name, out string value, out VariableScopeKind scope, out bool isSecret)
    {
        (VariableScopeKind Kind, EnvironmentVariableSource Source)[] ordered =
        [
            (VariableScopeKind.Local, ListSource(Local ?? [])),
            (VariableScopeKind.Environment, new EnvironmentVariableSource(Environment)),
            (VariableScopeKind.Collection, ListSource(Collection)),
            (VariableScopeKind.Globals, ListSource(Globals)),
        ];

        foreach (var (kind, source) in ordered)
        {
            if (source.TryGet(name, out value, out isSecret))
            {
                scope = kind;
                return true;
            }
        }

        isSecret = false;
        if (new DynamicVariableSource().TryGet(name, out value))
        {
            scope = VariableScopeKind.Dynamic;
            return true;
        }

        value = "";
        scope = VariableScopeKind.Dynamic;
        return false;
    }

    private static EnvironmentVariableSource ListSource(IReadOnlyList<KeyValueEntry> entries) =>
        new(new EnvironmentDefinition(Guid.Empty, "", entries));
}
