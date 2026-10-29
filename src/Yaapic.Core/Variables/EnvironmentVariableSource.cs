using Yaapic.Domain.Environments;

namespace Yaapic.Core.Variables;

/// <summary>Resolves variables from the enabled entries of an environment.</summary>
public sealed class EnvironmentVariableSource : IVariableSource
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

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
            }
        }
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
