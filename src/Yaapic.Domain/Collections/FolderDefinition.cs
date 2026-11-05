using Yaapic.Domain.Http;

namespace Yaapic.Domain.Collections;

/// <summary>A folder of the collection tree, with its own variables.</summary>
/// <param name="Id">The stable identifier.</param>
/// <param name="Name">The display name.</param>
/// <param name="Description">The free-text description.</param>
/// <param name="Variables">The folder variables, in order.</param>
public sealed record FolderDefinition(Guid Id, string Name, string Description, IReadOnlyList<KeyValueEntry> Variables)
{
    /// <summary>Creates an empty folder with a new identifier.</summary>
    /// <param name="name">The display name; it is trimmed.</param>
    /// <returns>The new folder.</returns>
    /// <exception cref="ArgumentException"><paramref name="name"/> is null, empty or whitespace.</exception>
    public static FolderDefinition Create(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return new(Guid.NewGuid(), name.Trim(), "", []);
    }

    /// <summary>Compares two folders by value, including the contents of the variable list.</summary>
    /// <param name="other">The folder to compare with.</param>
    /// <returns><see langword="true"/> when every member is equal.</returns>
    public bool IsEquivalentTo(FolderDefinition other) =>
        Id == other.Id && Name == other.Name && Description == other.Description
        && Variables.SequenceEqual(other.Variables);
}
