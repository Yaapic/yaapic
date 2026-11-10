namespace Yaapic.Core.Secrets;

/// <summary>Wraps and unwraps the vault data key with a platform or passphrase mechanism.</summary>
public interface IKeyProtector
{
    /// <summary>Gets the protector identifier: <c>passphrase</c>, <c>dpapi</c>, <c>keychain</c> or <c>secret-service</c>.</summary>
    string Id { get; }

    /// <summary>Gets a value indicating whether the user must type a passphrase to unlock.</summary>
    bool RequiresPassphrase { get; }

    /// <summary>Tells whether this protector works on the current machine.</summary>
    /// <param name="reason">When unavailable, a user-presentable reason; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when available.</returns>
    bool IsAvailable(out string? reason);

    /// <summary>Wraps the data key.</summary>
    /// <param name="dataKey">The key to protect.</param>
    /// <param name="workspaceId">The workspace identifier, bound to the wrapped key.</param>
    /// <returns>The wrapped key.</returns>
    /// <exception cref="VaultException">The protector failed.</exception>
    WrappedKey Wrap(ReadOnlySpan<byte> dataKey, string workspaceId);

    /// <summary>Unwraps the data key.</summary>
    /// <param name="wrapped">The wrapped key.</param>
    /// <param name="workspaceId">The workspace identifier it was wrapped for.</param>
    /// <returns>The data key.</returns>
    /// <exception cref="VaultException">The key could not be unwrapped (wrong passphrase, protector failure).</exception>
    byte[] Unwrap(WrappedKey wrapped, string workspaceId);
}
