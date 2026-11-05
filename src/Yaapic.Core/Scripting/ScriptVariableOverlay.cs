using Yaapic.Core.Variables;
using Yaapic.Domain.Http;

namespace Yaapic.Core.Scripting;

/// <summary>
/// A mutable overlay over the environment, collection and globals that scripts read and write during one send.
/// It records every change to a persisted scope. Pure and synchronous: the host serialises calls, so it is not thread-safe.
/// </summary>
/// <param name="initial">The scopes at the start of the send.</param>
/// <param name="hasEnvironment">Whether an environment is active, so that writes to it are possible.</param>
/// <param name="hasCollection">Whether the request belongs to a collection, so that writes to it are possible.</param>
public sealed class ScriptVariableOverlay(VariableScopes initial, bool hasEnvironment, bool hasCollection)
{
    private readonly List<KeyValueEntry> _environment = hasEnvironment && initial.Environment is not null
        ? [.. initial.Environment.Variables]
        : [];

    private readonly List<KeyValueEntry> _collection = [.. initial.Collection];
    private readonly List<KeyValueEntry> _globals = [.. initial.Globals];
    private readonly Dictionary<string, string> _local = new(StringComparer.Ordinal);
    private readonly List<VariableChange> _changes = [];
    private readonly List<string> _warnings = [];

    /// <summary>Gets the changes made to persisted scopes, in first-write order; repeated writes keep the last value.</summary>
    public IReadOnlyList<VariableChange> Changes => _changes;

    /// <summary>Gets the warnings raised by writes that fell back to the local layer.</summary>
    public IReadOnlyList<string> Warnings => _warnings;

    /// <summary>Reads a variable.</summary>
    /// <param name="scope">The layer; <see cref="ScriptScope.Layered"/> merges them all, dynamic variables included.</param>
    /// <param name="name">The exact variable name.</param>
    /// <param name="value">The value when found; otherwise an empty string.</param>
    /// <returns><see langword="true"/> when an enabled variable of that name exists.</returns>
    public bool TryGet(ScriptScope scope, string name, out string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        switch (scope)
        {
            case ScriptScope.Local:
                return TryGetLocal(name, out value);
            case ScriptScope.Environment:
                return TryFind(_environment, name, out value);
            case ScriptScope.Collection:
                return TryFind(_collection, name, out value);
            case ScriptScope.Globals:
                return TryFind(_globals, name, out value);
            default:
                return TryGetLocal(name, out value)
                    || TryFind(_environment, name, out value)
                    || TryFind(_collection, name, out value)
                    || TryFind(_globals, name, out value)
                    || new DynamicVariableSource().TryGet(name, out value);
        }
    }

    /// <summary>Writes a variable.</summary>
    /// <param name="scope">The layer; <see cref="ScriptScope.Layered"/> writes the local layer.</param>
    /// <param name="name">The variable name.</param>
    /// <param name="value">The value.</param>
    public void Set(ScriptScope scope, string name, string value)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);
        switch (scope)
        {
            case ScriptScope.Environment when hasEnvironment:
                Write(_environment, VariableScopeKind.Environment, name, value);
                break;
            case ScriptScope.Collection when hasCollection:
                Write(_collection, VariableScopeKind.Collection, name, value);
                break;
            case ScriptScope.Globals:
                Write(_globals, VariableScopeKind.Globals, name, value);
                break;
            case ScriptScope.Environment:
                _local[name] = value;
                _warnings.Add($"No active environment: the value of '{name}' only lives for this send");
                break;
            case ScriptScope.Collection:
                _local[name] = value;
                _warnings.Add($"No collection: the value of '{name}' only lives for this send");
                break;
            default:
                _local[name] = value;
                break;
        }
    }

    /// <summary>Removes a variable; removing from a persisted scope records an unset change when it existed.</summary>
    /// <param name="scope">The layer; <see cref="ScriptScope.Layered"/> removes from the local layer.</param>
    /// <param name="name">The exact variable name.</param>
    public void Unset(ScriptScope scope, string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        switch (scope)
        {
            case ScriptScope.Environment when hasEnvironment:
                Remove(_environment, VariableScopeKind.Environment, name);
                break;
            case ScriptScope.Collection when hasCollection:
                Remove(_collection, VariableScopeKind.Collection, name);
                break;
            case ScriptScope.Globals:
                Remove(_globals, VariableScopeKind.Globals, name);
                break;
            case ScriptScope.Environment or ScriptScope.Collection:
                break;
            default:
                _local.Remove(name);
                break;
        }
    }

    /// <summary>Removes every variable of a layer; persisted scopes record one unset per enabled row.</summary>
    /// <param name="scope">The layer; <see cref="ScriptScope.Layered"/> clears the local layer.</param>
    public void Clear(ScriptScope scope)
    {
        switch (scope)
        {
            case ScriptScope.Environment when hasEnvironment:
                RemoveAll(_environment, VariableScopeKind.Environment);
                break;
            case ScriptScope.Collection when hasCollection:
                RemoveAll(_collection, VariableScopeKind.Collection);
                break;
            case ScriptScope.Globals:
                RemoveAll(_globals, VariableScopeKind.Globals);
                break;
            case ScriptScope.Environment or ScriptScope.Collection:
                break;
            default:
                _local.Clear();
                break;
        }
    }

    /// <summary>Lists the enabled variables of a layer; dynamic variables are never listed.</summary>
    /// <param name="scope">The layer; <see cref="ScriptScope.Layered"/> merges them with the highest layer winning.</param>
    /// <returns>The names and values.</returns>
    public IReadOnlyDictionary<string, string> List(ScriptScope scope)
    {
        Dictionary<string, string> result = new(StringComparer.Ordinal);
        switch (scope)
        {
            case ScriptScope.Local:
                AddAll(result, ToEntries());
                break;
            case ScriptScope.Environment:
                AddAll(result, _environment);
                break;
            case ScriptScope.Collection:
                AddAll(result, _collection);
                break;
            case ScriptScope.Globals:
                AddAll(result, _globals);
                break;
            default:
                // lowest layer first so that higher layers overwrite
                AddAll(result, _globals);
                AddAll(result, _collection);
                AddAll(result, _environment);
                AddAll(result, ToEntries());
                break;
        }

        return result;
    }

    /// <summary>Replaces <c>{{ name }}</c> placeholders with the layered values; unresolved ones stay as written.</summary>
    /// <param name="input">The text.</param>
    /// <returns>The resolved text.</returns>
    public string ReplaceIn(string input) => VariableResolver.Resolve(input, ToScopes().CreateSource()).Text;

    /// <summary>Builds the current scopes, including the local layer.</summary>
    /// <returns>The scopes for the normal resolver.</returns>
    public VariableScopes ToScopes() => new(
        hasEnvironment && initial.Environment is { } env ? env with { Variables = [.. _environment] } : null,
        [.. _collection],
        [.. _globals],
        ToEntries());

    private static void AddAll(Dictionary<string, string> target, IReadOnlyList<KeyValueEntry> rows)
    {
        foreach (var row in rows)
        {
            if (row.Enabled && row.Key.Length > 0)
            {
                target[row.Key] = row.Value;
            }
        }
    }

    // The last enabled row wins, like the resolver.
    private static bool TryFind(List<KeyValueEntry> rows, string name, out string value)
    {
        for (var i = rows.Count - 1; i >= 0; i--)
        {
            if (rows[i].Enabled && rows[i].Key == name)
            {
                value = rows[i].Value;
                return true;
            }
        }

        value = "";
        return false;
    }

    private List<KeyValueEntry> ToEntries() => [.. _local.Select(p => new KeyValueEntry(p.Key, p.Value))];

    private bool TryGetLocal(string name, out string value)
    {
        if (_local.TryGetValue(name, out var found))
        {
            value = found;
            return true;
        }

        value = "";
        return false;
    }

    private void Write(List<KeyValueEntry> rows, VariableScopeKind scope, string name, string value)
    {
        var index = rows.FindLastIndex(r => r.Key == name);
        if (index >= 0)
        {
            rows[index] = rows[index] with { Value = value, Enabled = true };
        }
        else
        {
            rows.Add(new KeyValueEntry(name, value));
        }

        Record(new VariableChange(scope, name, value));
    }

    private void Remove(List<KeyValueEntry> rows, VariableScopeKind scope, string name)
    {
        if (rows.RemoveAll(r => r.Key == name) > 0)
        {
            Record(new VariableChange(scope, name, null));
        }
    }

    private void RemoveAll(List<KeyValueEntry> rows, VariableScopeKind scope)
    {
        foreach (var row in rows.Where(r => r.Enabled && r.Key.Length > 0).ToList())
        {
            Record(new VariableChange(scope, row.Key, null));
        }

        rows.Clear();
    }

    private void Record(VariableChange change)
    {
        var index = _changes.FindIndex(c => c.Scope == change.Scope && c.Name == change.Name);
        if (index >= 0)
        {
            _changes[index] = change;
        }
        else
        {
            _changes.Add(change);
        }
    }
}
