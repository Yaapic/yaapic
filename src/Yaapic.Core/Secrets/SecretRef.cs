namespace Yaapic.Core.Secrets;

/// <summary>Identifies one secret: the owner it belongs to and its variable name.</summary>
/// <param name="Kind">The kind of owner.</param>
/// <param name="OwnerId">The owner identifier; <see cref="Guid.Empty"/> for the globals.</param>
/// <param name="Name">The variable name.</param>
public readonly record struct SecretRef(SecretOwnerKind Kind, Guid OwnerId, string Name)
{
    /// <summary>Creates a reference to a global secret.</summary>
    /// <param name="name">The variable name.</param>
    /// <returns>The reference, with owner <see cref="Guid.Empty"/>.</returns>
    public static SecretRef Globals(string name) => new(SecretOwnerKind.Globals, Guid.Empty, name);
}
