using Yaapic.Core.Secrets;

namespace Yaapic.Infrastructure.Secrets;

/// <summary>Offers the Keychain (macOS) or the Secret Service (Linux) first when available, then the passphrase protector.</summary>
public sealed class UnixKeyProtectorFactory : IKeyProtectorFactory
{
    private readonly PassphraseKeyProtectorFactory _passphrase;
    private readonly IKeyProtector _platform;

    /// <summary>Creates a factory with the production passphrase iteration count.</summary>
    public UnixKeyProtectorFactory()
        : this(PassphraseKdf.DefaultIterations, OperatingSystem.IsMacOS() ? new MacKeychainKeyProtector() : new SecretToolKeyProtector())
    {
    }

    internal UnixKeyProtectorFactory(int iterations, IKeyProtector platform)
    {
        _passphrase = new PassphraseKeyProtectorFactory(iterations);
        _platform = platform;
    }

    /// <inheritdoc />
    public IReadOnlyList<IKeyProtector> Available() =>
        _platform.IsAvailable(out _) ? [_platform, .. _passphrase.Available()] : _passphrase.Available();

    /// <inheritdoc />
    public IKeyProtector? Create(string id, string? passphrase)
    {
        if (id != _platform.Id)
        {
            return _passphrase.Create(id, passphrase);
        }

        return _platform.IsAvailable(out _) ? _platform : null;
    }
}
