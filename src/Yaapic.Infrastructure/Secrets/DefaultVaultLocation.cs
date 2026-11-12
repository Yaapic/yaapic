namespace Yaapic.Infrastructure.Secrets;

/// <summary>Stores vaults in <c>&lt;user data dir&gt;/Yaapic/vaults/&lt;id&gt;.vault</c>, outside every workspace.</summary>
/// <param name="userDataDirectory">Replaces the per-user data directory (screenshot captures use a throw-away one); <see langword="null"/> for the real one.</param>
public sealed class DefaultVaultLocation(string? userDataDirectory = null) : IVaultLocation
{
    /// <inheritdoc />
    public string GetVaultPath(string workspaceId) =>
        Path.Combine(userDataDirectory ?? UserDataDirectory(), "Yaapic", "vaults", Guid.ParseExact(workspaceId, "D").ToString("D") + ".vault");

    internal static string UserDataDirectory()
    {
        if (OperatingSystem.IsWindows())
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (OperatingSystem.IsMacOS())
        {
            return Path.Combine(home, "Library", "Application Support");
        }

        var xdg = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        return string.IsNullOrEmpty(xdg) ? Path.Combine(home, ".local", "share") : xdg;
    }
}
