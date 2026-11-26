using Yaapic.Core.Scripting;
using Yaapic.Core.Variables;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Runner;

/// <summary>Keeps the variable changes of one run in memory so later requests see them, whether or not they are persisted. Not thread-safe.</summary>
public sealed class RunVariableState
{
    private readonly List<VariableChange> _changes = [];

    /// <summary>Gets a value indicating whether no change was recorded.</summary>
    public bool IsEmpty => _changes.Count == 0;

    /// <summary>Records changes in order; only the environment, collection and globals scopes are kept.</summary>
    /// <param name="changes">The changes a script made.</param>
    public void Apply(IReadOnlyList<VariableChange> changes)
    {
        foreach (VariableChange change in changes)
        {
            if (change.Scope is VariableScopeKind.Environment or VariableScopeKind.Collection or VariableScopeKind.Globals)
            {
                _changes.Add(change);
            }
        }
    }

    /// <summary>Returns a copy of the scopes with the recorded changes applied and the iteration data row attached.</summary>
    /// <param name="scopes">The persisted scopes; never mutated.</param>
    /// <param name="row">The iteration data row, exposed as <see cref="VariableScopes.Data"/>.</param>
    /// <returns>The overlaid scopes.</returns>
    public VariableScopes Overlay(VariableScopes scopes, IterationDataRow row)
    {
        var environment = scopes.Environment is { } env
            ? env with { Variables = Fold(env.Variables, VariableScopeKind.Environment) }
            : null;
        return scopes with
        {
            Environment = environment,
            Collection = Fold(scopes.Collection, VariableScopeKind.Collection),
            Globals = Fold(scopes.Globals, VariableScopeKind.Globals),
            Data = row.Values,
        };
    }

    private List<KeyValueEntry> Fold(IReadOnlyList<KeyValueEntry> source, VariableScopeKind scope)
    {
        List<KeyValueEntry> list = [.. source];
        foreach (VariableChange change in _changes)
        {
            if (change.Scope != scope)
            {
                continue;
            }

            if (change.Value is null)
            {
                list.RemoveAll(e => e.Key == change.Name);
                continue;
            }

            int index = list.FindIndex(e => e.Key == change.Name && e.Enabled);
            if (index >= 0)
            {
                list[index] = list[index] with { Value = change.Value };
            }
            else
            {
                list.Add(new KeyValueEntry(change.Name, change.Value));
            }
        }

        return list;
    }
}
