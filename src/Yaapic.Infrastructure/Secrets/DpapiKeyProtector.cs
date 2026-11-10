using System.Security.Cryptography;
using System.Text;
using Yaapic.Core.Secrets;

namespace Yaapic.Infrastructure.Secrets;

/// <summary>Wraps the data key with Windows DPAPI for the current user, bound to the workspace through the entropy.</summary>
public sealed class DpapiKeyProtector : IKeyProtector
{
    /// <summary>The protector identifier.</summary>
    public const string ProtectorId = "dpapi";

    /// <inheritdoc />
    public string Id => ProtectorId;

    /// <inheritdoc />
    public bool RequiresPassphrase => false;

    /// <inheritdoc />
    public bool IsAvailable(out string? reason)
    {
        if (!OperatingSystem.IsWindows())
        {
            reason = "Windows data protection is only available on Windows";
            return false;
        }

        try
        {
            byte[] probe = [1];
            var blob = ProtectedData.Protect(probe, null, DataProtectionScope.CurrentUser);
            reason = null;
            return ProtectedData.Unprotect(blob, null, DataProtectionScope.CurrentUser).Length == 1;
        }
        catch (Exception e) when (e is CryptographicException or PlatformNotSupportedException)
        {
            reason = "Windows data protection is not available for this user";
            return false;
        }
    }

    /// <inheritdoc />
    public WrappedKey Wrap(ReadOnlySpan<byte> dataKey, string workspaceId)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new VaultException("Windows data protection is only available on Windows");
        }

        try
        {
            return new WrappedKey(ProtectedData.Protect(dataKey.ToArray(), Entropy(workspaceId), DataProtectionScope.CurrentUser), null);
        }
        catch (Exception e) when (e is CryptographicException or PlatformNotSupportedException)
        {
            throw new VaultException("Windows could not protect the vault for this user", e);
        }
    }

    /// <inheritdoc />
    public byte[] Unwrap(WrappedKey wrapped, string workspaceId)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new VaultException("Windows data protection is only available on Windows");
        }

        try
        {
            return ProtectedData.Unprotect(wrapped.Bytes, Entropy(workspaceId), DataProtectionScope.CurrentUser);
        }
        catch (Exception e) when (e is CryptographicException or PlatformNotSupportedException)
        {
            throw new VaultException("Windows could not unlock the vault for this user", e);
        }
    }

    private static byte[] Entropy(string workspaceId) => Encoding.UTF8.GetBytes("yaapic-vault-v1|" + workspaceId);
}
