using Yaapic.Core.Secrets;

namespace Yaapic.Infrastructure.Secrets;

/// <summary>Offers only the passphrase protector; platform factories wrap it and add their own first.</summary>
public sealed class PassphraseKeyProtectorFactory : IKeyProtectorFactory
{
    private readonly int _iterations;

    /// <summary>Creates a factory with the production iteration count.</summary>
    public PassphraseKeyProtectorFactory()
        : this(PassphraseKdf.DefaultIterations)
    {
    }

    internal PassphraseKeyProtectorFactory(int iterations) => _iterations = iterations;

    /// <inheritdoc />
    public IReadOnlyList<IKeyProtector> Available() => [new PassphraseKeyProtector("", _iterations)];

    /// <inheritdoc />
    public IKeyProtector? Create(string id, string? passphrase) =>
        id == PassphraseKeyProtector.ProtectorId ? new PassphraseKeyProtector(passphrase ?? "", _iterations) : null;
}
