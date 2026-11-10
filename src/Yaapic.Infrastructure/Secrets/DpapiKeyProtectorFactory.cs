using Yaapic.Core.Secrets;

namespace Yaapic.Infrastructure.Secrets;

/// <summary>Offers DPAPI first on Windows, then the passphrase protector.</summary>
public sealed class DpapiKeyProtectorFactory : IKeyProtectorFactory
{
    private readonly PassphraseKeyProtectorFactory _passphrase;

    /// <summary>Creates a factory with the production passphrase iteration count.</summary>
    public DpapiKeyProtectorFactory() => _passphrase = new PassphraseKeyProtectorFactory();

    internal DpapiKeyProtectorFactory(int iterations) => _passphrase = new PassphraseKeyProtectorFactory(iterations);

    /// <inheritdoc />
    public IReadOnlyList<IKeyProtector> Available()
    {
        var dpapi = new DpapiKeyProtector();
        return dpapi.IsAvailable(out _) ? [dpapi, .. _passphrase.Available()] : _passphrase.Available();
    }

    /// <inheritdoc />
    public IKeyProtector? Create(string id, string? passphrase)
    {
        if (id != DpapiKeyProtector.ProtectorId)
        {
            return _passphrase.Create(id, passphrase);
        }

        var dpapi = new DpapiKeyProtector();
        return dpapi.IsAvailable(out _) ? dpapi : null;
    }
}
