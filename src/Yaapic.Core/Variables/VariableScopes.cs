using Yaapic.Domain.Environments;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Variables;

/// <summary>The variable scopes a request is resolved against: environment, collection, globals, then dynamic.</summary>
/// <param name="Environment">The active environment, or <see langword="null"/> for none.</param>
/// <param name="Collection">The collection variables.</param>
/// <param name="Globals">The global variables.</param>
public sealed record VariableScopes(
    EnvironmentDefinition? Environment,
    IReadOnlyList<KeyValueEntry> Collection,
    IReadOnlyList<KeyValueEntry> Globals)
{
    /// <summary>Gets scopes that define nothing besides the dynamic variables.</summary>
    public static VariableScopes None { get; } = new(null, [], []);

    /// <summary>Creates a source that asks the environment, the collection, the globals, then the dynamic variables.</summary>
    /// <returns>The composite source.</returns>
    public IVariableSource CreateSource() =>
        new CompositeVariableSource(
            new EnvironmentVariableSource(Environment),
            ListSource(Collection),
            ListSource(Globals),
            new DynamicVariableSource());

    /// <summary>Resolves a variable and reports the scope it came from.</summary>
    /// <param name="name">The exact, case-sensitive variable name.</param>
    /// <param name="value">The value when found; otherwise an empty string.</param>
    /// <param name="scope">The scope of the value when found; otherwise <see cref="VariableScopeKind.Dynamic"/>.</param>
    /// <returns><see langword="true"/> when the variable is defined in any scope.</returns>
    public bool TryResolve(string name, out string value, out VariableScopeKind scope)
    {
        (VariableScopeKind Kind, IVariableSource Source)[] ordered =
        [
            (VariableScopeKind.Environment, new EnvironmentVariableSource(Environment)),
            (VariableScopeKind.Collection, ListSource(Collection)),
            (VariableScopeKind.Globals, ListSource(Globals)),
            (VariableScopeKind.Dynamic, new DynamicVariableSource()),
        ];

        foreach (var (kind, source) in ordered)
        {
            if (source.TryGet(name, out value))
            {
                scope = kind;
                return true;
            }
        }

        value = "";
        scope = VariableScopeKind.Dynamic;
        return false;
    }

    private static EnvironmentVariableSource ListSource(IReadOnlyList<KeyValueEntry> entries) =>
        new(new EnvironmentDefinition(Guid.Empty, "", entries));
}
