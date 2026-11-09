using Yaapic.Core.Secrets;

namespace Yaapic.TestDoubles;

/// <summary>An <see cref="IKeyProtectorFactory"/> over a fixed list of <see cref="FakeKeyProtector"/>s.</summary>
/// <param name="protectors">The protectors, platform one first.</param>
public sealed class FakeKeyProtectorFactory(params FakeKeyProtector[] protectors) : IKeyProtectorFactory
{
    /// <summary>Gets the protectors created so far, with the passphrase each received.</summary>
    public List<(string Id, string? Passphrase)> Created { get; } = [];

    /// <inheritdoc/>
    public IReadOnlyList<IKeyProtector> Available() => [.. protectors.Where(p => p.IsAvailable(out _))];

    /// <inheritdoc/>
    public IKeyProtector? Create(string id, string? passphrase)
    {
        Created.Add((id, passphrase));
        return protectors.FirstOrDefault(p => p.Id == id && p.IsAvailable(out _));
    }
}

/// <summary>An <see cref="IKeyProtector"/> that wraps by reversing the bytes; no cryptography.</summary>
/// <param name="id">The protector identifier.</param>
/// <param name="requiresPassphrase">Whether a passphrase is required.</param>
/// <param name="unavailableReason">Why the protector is unavailable; <see langword="null"/> when available.</param>
public sealed class FakeKeyProtector(string id, bool requiresPassphrase = false, string? unavailableReason = null) : IKeyProtector
{
    /// <inheritdoc/>
    public string Id => id;

    /// <inheritdoc/>
    public bool RequiresPassphrase => requiresPassphrase;

    /// <inheritdoc/>
    public bool IsAvailable(out string? reason)
    {
        reason = unavailableReason;
        return unavailableReason is null;
    }

    /// <inheritdoc/>
    public WrappedKey Wrap(ReadOnlySpan<byte> dataKey, string workspaceId) => new([.. dataKey.ToArray().Reverse()], null);

    /// <inheritdoc/>
    public byte[] Unwrap(WrappedKey wrapped, string workspaceId) => [.. wrapped.Bytes.Reverse()];
}
