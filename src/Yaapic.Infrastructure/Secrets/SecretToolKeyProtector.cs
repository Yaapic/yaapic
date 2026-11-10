using System.Text;
using Yaapic.Core.Secrets;

namespace Yaapic.Infrastructure.Secrets;

/// <summary>
/// Keeps the vault data key in the Linux Secret Service through <c>secret-tool</c> (base64 on stdin, never in arguments);
/// the wrapped key is only a marker because the key itself lives in the Secret Service.
/// </summary>
public sealed class SecretToolKeyProtector : IKeyProtector
{
    /// <summary>The protector identifier.</summary>
    public const string ProtectorId = "secret-service";

    private static readonly byte[] Marker = Encoding.ASCII.GetBytes("secret-service-v1");

    private readonly ISecretTool _tool;
    private readonly bool _skipOsCheck;

    /// <summary>Creates a protector that runs the real <c>secret-tool</c>.</summary>
    public SecretToolKeyProtector()
        : this(new ProcessSecretTool(), false)
    {
    }

    internal SecretToolKeyProtector(ISecretTool tool, bool skipOsCheck)
    {
        _tool = tool;
        _skipOsCheck = skipOsCheck;
    }

    /// <inheritdoc />
    public string Id => ProtectorId;

    /// <inheritdoc />
    public bool RequiresPassphrase => false;

    /// <inheritdoc />
    public bool IsAvailable(out string? reason)
    {
        if (!_skipOsCheck && !OperatingSystem.IsLinux())
        {
            reason = "The Secret Service is only available on Linux";
            return false;
        }

        if (!_tool.Exists())
        {
            reason = "secret-tool is not installed";
            return false;
        }

        if (_tool.Run(["lookup", "service", "Yaapic", "account", "__probe__"], null).ExitCode is not (0 or 1))
        {
            reason = "The Secret Service is not reachable (no D-Bus session or keyring locked)";
            return false;
        }

        reason = null;
        return true;
    }

    /// <inheritdoc />
    public WrappedKey Wrap(ReadOnlySpan<byte> dataKey, string workspaceId)
    {
        var result = _tool.Run(["store", "--label", "Yaapic vault", "service", "Yaapic", "account", workspaceId], Convert.ToBase64String(dataKey));
        if (result.ExitCode != 0)
        {
            throw new VaultException("The Secret Service refused to store the vault key");
        }

        return new WrappedKey(Marker, null);
    }

    /// <inheritdoc />
    public byte[] Unwrap(WrappedKey wrapped, string workspaceId)
    {
        if (!wrapped.Bytes.AsSpan().SequenceEqual(Marker))
        {
            throw new VaultException("The vault was not protected by the Secret Service");
        }

        var result = _tool.Run(["lookup", "service", "Yaapic", "account", workspaceId], null);
        if (result.ExitCode == 0 && Decode(result.Output) is { Length: 32 } key)
        {
            return key;
        }

        throw new VaultException("The Secret Service refused access to the vault key");
    }

    private static byte[]? Decode(string output)
    {
        var text = output.Trim();
        var buffer = new byte[text.Length];
        return Convert.TryFromBase64String(text, buffer, out var written) ? buffer[..written] : null;
    }
}
