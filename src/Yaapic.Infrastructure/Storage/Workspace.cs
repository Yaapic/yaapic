using System.Text;
using Yaapic.Core.Storage;

namespace Yaapic.Infrastructure.Storage;

/// <summary>Locates and bootstraps a workspace folder.</summary>
public static class Workspace
{
    /// <summary>Gets the default workspace folder, <c>~/Yaapic/Default</c>.</summary>
    /// <returns>The absolute path of the default workspace.</returns>
    public static string DefaultRoot() =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Yaapic", "Default");

    /// <summary>Creates the workspace folders and a <c>.gitignore</c> if none exists. Idempotent.</summary>
    /// <param name="root">The workspace root directory.</param>
    /// <exception cref="StorageException">The workspace cannot be created.</exception>
    public static void EnsureCreated(string root)
    {
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "requests"));
            Directory.CreateDirectory(Path.Combine(root, "environments"));
            Directory.CreateDirectory(Path.Combine(root, ".yaapic"));
            var gitignore = Path.Combine(root, ".gitignore");
            if (!File.Exists(gitignore))
            {
                File.WriteAllText(gitignore, ".yaapic/\n", new UTF8Encoding(false));
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new StorageException($"Cannot create workspace '{root}': {ex.Message}", ex);
        }
    }
}
