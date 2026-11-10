namespace Yaapic.Infrastructure.Secrets;

/// <summary>Tells where the vault file of a workspace lives.</summary>
public interface IVaultLocation
{
    /// <summary>Gets the vault file path of a workspace.</summary>
    /// <param name="workspaceId">The workspace GUID.</param>
    /// <returns>The absolute path of the <c>.vault</c> file.</returns>
    /// <exception cref="FormatException"><paramref name="workspaceId"/> is not a GUID.</exception>
    string GetVaultPath(string workspaceId);
}
