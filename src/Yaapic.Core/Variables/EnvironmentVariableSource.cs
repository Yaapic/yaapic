using Yaapic.Domain.Environments;

namespace Yaapic.Core.Variables;

/// <summary>Resolves variables from the enabled entries of an environment.</summary>
public sealed class EnvironmentVariableSource : IVariableSource
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);
    private readonly HashSet<string> _secrets = new(StringComparer.Ordinal);

    /// <summary>Creates a source over an environment.</summary>
    /// <param name="environment">The environment; <see langword="null"/> resolves nothing.</param>
    public EnvironmentVariableSource(EnvironmentDefinition? environment)
    {
        if (environment is null)
        {
            return;
        }

        foreach (var entry in environment.Variables)
        {
            if (entry.Enabled && entry.Key.Length > 0)
            {
                _values[entry.Key] = entry.Value; // the last enabled entry wins
                if (entry.IsSecret)
                {
                    _secrets.Add(entry.Key);
                }
                else
                {
                    _secrets.Remove(entry.Key);
                }
            }
        }
    }

    /// <summary>Looks a variable up and tells whether the winning row is a secret.</summary>
    /// <param name="name">The exact, case-sensitive variable name.</param>
    /// <param name="value">The value when found; otherwise an empty string.</param>
    /// <param name="isSecret">Whether the row is secret; <see langword="false"/> when not found.</param>
    /// <returns><see langword="true"/> when the variable is defined.</returns>
    public bool TryGet(string name, out string value, out bool isSecret)
    {
        isSecret = _secrets.Contains(name);
        return TryGet(name, out value);
    }

    /// <inheritdoc />
    public bool TryGet(string name, out string value)
    {
        if (_values.TryGetValue(name, out var found))
        {
            value = found;
            return true;
        }

        value = "";
        return false;
    }
}
