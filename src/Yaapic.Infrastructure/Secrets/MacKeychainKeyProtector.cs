using System.Text;
using Yaapic.Core.Secrets;

namespace Yaapic.Infrastructure.Secrets;

/// <summary>
/// Keeps the vault data key in the macOS Keychain (generic password, service <c>Yaapic</c>, account <c>&lt;workspaceId&gt;</c>);
/// the wrapped key is only a marker because the key itself lives in the Keychain.
/// </summary>
public sealed class MacKeychainKeyProtector : IKeyProtector
{
    /// <summary>The protector identifier.</summary>
    public const string ProtectorId = "keychain";

    private const string Service = "Yaapic";
    private static readonly byte[] Marker = Encoding.ASCII.GetBytes("keychain-v1");

    private readonly IKeychainApi? _api;

    /// <summary>Creates a protector that talks to the real Keychain (macOS only).</summary>
    public MacKeychainKeyProtector()
    {
    }

    internal MacKeychainKeyProtector(IKeychainApi api) => _api = api;

    /// <inheritdoc />
    public string Id => ProtectorId;

    /// <inheritdoc />
    public bool RequiresPassphrase => false;

    /// <inheritdoc />
    public bool IsAvailable(out string? reason)
    {
        if (_api is null && !OperatingSystem.IsMacOS())
        {
            reason = "The macOS Keychain is only available on macOS";
            return false;
        }

        reason = null;
        return true;
    }

    /// <inheritdoc />
    public WrappedKey Wrap(ReadOnlySpan<byte> dataKey, string workspaceId)
    {
        if (Api().Store(Service, workspaceId, dataKey.ToArray()) != 0)
        {
            throw new VaultException("The macOS Keychain refused access");
        }

        return new WrappedKey(Marker, null);
    }

    /// <inheritdoc />
    public byte[] Unwrap(WrappedKey wrapped, string workspaceId)
    {
        if (!wrapped.Bytes.AsSpan().SequenceEqual(Marker))
        {
            throw new VaultException("The vault was not protected by the macOS Keychain");
        }

        if (Api().Read(Service, workspaceId, out var key) != 0 || key is not { Length: 32 })
        {
            throw new VaultException("The macOS Keychain refused access");
        }

        return key;
    }

    private IKeychainApi Api()
    {
        if (_api is not null)
        {
            return _api;
        }

        if (!OperatingSystem.IsMacOS())
        {
            throw new VaultException("The macOS Keychain is only available on macOS");
        }

        return new SecKeychainApi();
    }
}
