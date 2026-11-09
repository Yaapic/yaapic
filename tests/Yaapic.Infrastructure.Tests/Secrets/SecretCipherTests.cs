using System.Security.Cryptography;
using System.Text;
using Yaapic.Core.Secrets;
using Yaapic.Infrastructure.Secrets;

namespace Yaapic.Infrastructure.Tests.Secrets;

public sealed class SecretCipherTests
{
    private static readonly Guid Owner = Guid.Parse("6f1c2a3b-4d5e-4f60-8172-93a4b5c6d7e8");
    private static readonly SecretRef Ref = new(SecretOwnerKind.Environment, Owner, "token");

    public static TheoryData<string> Values() => new()
    {
        "",
        "hunter2",
        "pässwörd ☕ 日本語 😀",
        new string('x', 1024 * 1024),
    };

    [Theory]
    [MemberData(nameof(Values))]
    public void RoundTrips(string plaintext)
    {
        var key = DataKey.Generate();
        Assert.Equal(plaintext, SecretCipher.Decrypt(key, Ref, SecretCipher.Encrypt(key, Ref, plaintext)));
    }

    [Fact]
    public void NonceDiffersBetweenEncryptions()
    {
        var key = DataKey.Generate();
        var a = SecretCipher.Encrypt(key, Ref, "v");
        var b = SecretCipher.Encrypt(key, Ref, "v");
        Assert.NotEqual(a.Nonce, b.Nonce);
        Assert.NotEqual(a.Ciphertext, b.Ciphertext);
        Assert.Equal(12, a.Nonce.Length);
        Assert.Equal(16, a.Tag.Length);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void FlippingAnyByteFails(int part)
    {
        var key = DataKey.Generate();
        var value = SecretCipher.Encrypt(key, Ref, "secret value");
        var bytes = part switch { 0 => value.Nonce, 1 => value.Ciphertext, _ => value.Tag };
        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] ^= 0x01;
            Assert.Throws<VaultException>(() => SecretCipher.Decrypt(key, Ref, value));
            bytes[i] ^= 0x01;
        }

        Assert.Equal("secret value", SecretCipher.Decrypt(key, Ref, value));
    }

    [Fact]
    public void DifferentSecretRefFails()
    {
        var key = DataKey.Generate();
        var value = SecretCipher.Encrypt(key, Ref, "v");
        Assert.Throws<VaultException>(() => SecretCipher.Decrypt(key, Ref with { Name = "other" }, value));
        Assert.Throws<VaultException>(() => SecretCipher.Decrypt(key, Ref with { OwnerId = Guid.NewGuid() }, value));
        Assert.Throws<VaultException>(() => SecretCipher.Decrypt(key, Ref with { Kind = SecretOwnerKind.Collection }, value));
    }

    [Fact]
    public void WrongKeyFailsWithMessage()
    {
        var value = SecretCipher.Encrypt(DataKey.Generate(), Ref, "v");
        var e = Assert.Throws<VaultException>(() => SecretCipher.Decrypt(DataKey.Generate(), Ref, value));
        Assert.Equal("The vault entry cannot be decrypted", e.Message);
    }

    [Fact]
    public void ClearZeroesTheKey()
    {
        var key = DataKey.Generate();
        Assert.Equal(32, key.Length);
        DataKey.Clear(key);
        Assert.All(key, b => Assert.Equal(0, b));
    }

    [Fact]
    public void Pbkdf2MatchesPublishedSha512Vector()
    {
        // PBKDF2-HMAC-SHA512, P="password", S="salt", c=1, dkLen=64 (published test vector).
        var dk = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes("password"), Encoding.UTF8.GetBytes("salt"), 1, HashAlgorithmName.SHA512, 64);
        Assert.Equal(
            "867f70cf1ade02cff3752599a3a53dc4af34c7a669815ae5d513554e1c8cf252c02d470a285a0501bad999bfe943c08f050235d7d68b1da55e63f73b60a57fce",
            Convert.ToHexStringLower(dk));
    }

    [Fact]
    public void WrapUnwrapRoundTrips()
    {
        var key = DataKey.Generate();
        var wrapped = PassphraseKdf.Wrap(key, "correct horse");
        Assert.Equal("pbkdf2-sha512", wrapped.Kdf!.Algorithm);
        Assert.Equal(600_000, wrapped.Kdf.Iterations);
        Assert.Equal(16, wrapped.Kdf.Salt.Length);
        Assert.Equal(12 + 32 + 16, wrapped.Bytes.Length);
        Assert.Equal(key, PassphraseKdf.Unwrap(wrapped, "correct horse"));
    }

    [Fact]
    public void WrongPassphraseFails()
    {
        var wrapped = PassphraseKdf.Wrap(DataKey.Generate(), "right");
        var e = Assert.Throws<VaultException>(() => PassphraseKdf.Unwrap(wrapped, "wrong"));
        Assert.Equal("Wrong passphrase", e.Message);
    }

    [Fact]
    public void EmptyPassphraseIsRejected()
    {
        Assert.Throws<ArgumentException>(() => PassphraseKdf.Wrap(DataKey.Generate(), ""));
        var wrapped = PassphraseKdf.Wrap(DataKey.Generate(), "x");
        Assert.Throws<ArgumentException>(() => PassphraseKdf.Unwrap(wrapped, ""));
    }

    [Fact]
    public void UnknownAlgorithmIsUnsupported()
    {
        var w = PassphraseKdf.Wrap(DataKey.Generate(), "p");
        var bad = w with { Kdf = w.Kdf! with { Algorithm = "scrypt" } };
        Assert.Equal("Unsupported vault key derivation", Assert.Throws<VaultException>(() => PassphraseKdf.Unwrap(bad, "p")).Message);
        Assert.Throws<VaultException>(() => PassphraseKdf.Unwrap(w with { Kdf = null }, "p"));
    }

    [Theory]
    [InlineData(99_999)]
    [InlineData(10_000_001)]
    [InlineData(0)]
    public void IterationsOutOfBoundsAreUnsupported(int iterations)
    {
        var w = PassphraseKdf.Wrap(DataKey.Generate(), "p");
        var bad = w with { Kdf = w.Kdf! with { Iterations = iterations } };
        Assert.Equal("Unsupported vault key derivation", Assert.Throws<VaultException>(() => PassphraseKdf.Unwrap(bad, "p")).Message);
    }

    [Fact]
    public void LowerIterationBoundIsAccepted()
    {
        var w = PassphraseKdf.Wrap(DataKey.Generate(), "p");
        var low = w with { Kdf = w.Kdf! with { Iterations = 100_000 } };
        var e = Assert.Throws<VaultException>(() => PassphraseKdf.Unwrap(low, "p"));
        Assert.Equal("Wrong passphrase", e.Message); // bound accepted, then the tag check fails
    }

    [Fact]
    public void TamperedWrappedKeyFails()
    {
        var w = PassphraseKdf.Wrap(DataKey.Generate(), "p");
        w.Bytes[20] ^= 1;
        Assert.Throws<VaultException>(() => PassphraseKdf.Unwrap(w, "p"));
    }
}
