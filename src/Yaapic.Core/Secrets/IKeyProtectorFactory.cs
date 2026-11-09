namespace Yaapic.Core.Secrets;

/// <summary>Creates the key protectors available on this platform.</summary>
public interface IKeyProtectorFactory
{
    /// <summary>Lists the available protectors: the platform protector first, the passphrase protector last.</summary>
    /// <returns>The available protectors.</returns>
    IReadOnlyList<IKeyProtector> Available();

    /// <summary>Creates a protector by identifier.</summary>
    /// <param name="id">The protector identifier.</param>
    /// <param name="passphrase">The passphrase, for the passphrase protector; otherwise ignored.</param>
    /// <returns>The protector, or <see langword="null"/> when the identifier is unknown or unavailable.</returns>
    IKeyProtector? Create(string id, string? passphrase);
}
