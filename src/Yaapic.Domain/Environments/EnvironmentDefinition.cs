using Yaapic.Domain.Http;

namespace Yaapic.Domain.Environments;

/// <summary>A named set of variables; in M1 the single active environment is the only variable scope.</summary>
/// <param name="Id">The stable identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="Variables">The variables, in order; <see cref="KeyValueEntry.Key"/> is the case-sensitive name.</param>
public sealed record EnvironmentDefinition(Guid Id, string Name, IReadOnlyList<KeyValueEntry> Variables)
{
    /// <summary>Creates an empty environment with a new identifier.</summary>
    /// <param name="name">The display name; it is trimmed.</param>
    /// <returns>The new environment.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty or whitespace.</exception>
    public static EnvironmentDefinition Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new(Guid.NewGuid(), name.Trim(), []);
    }

    /// <summary>Compares two environments by value, including the contents of the variable list.</summary>
    /// <param name="other">The environment to compare with.</param>
    /// <returns><see langword="true"/> when every member is equal.</returns>
    public bool IsEquivalentTo(EnvironmentDefinition other) =>
        Id == other.Id && Name == other.Name && Variables.SequenceEqual(other.Variables);
}
