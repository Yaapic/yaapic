using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Yaapic.Core.Certificates;
using Yaapic.Core.Secrets;
using Yaapic.Domain.Http;
using Yaapic.Infrastructure.Certificates;
using Yaapic.TestDoubles;

namespace Yaapic.Infrastructure.Tests.Certificates;

public sealed class ClientCertificateProviderTests : IDisposable
{
    private const string Secret = "s3cret-passphrase";
    private static readonly Uri Url = new("https://api.example.com/x");

    private readonly string _dir = Path.Combine(Path.GetTempPath(), "yaapic-certload-" + Guid.NewGuid().ToString("N"));
    private readonly InMemoryCertificateStore _store = new();
    private readonly InMemorySecretVault _vault = new();
    private readonly ClientCertificateProvider _provider;
    private readonly RSA _rsa = RSA.Create(2048);
    private readonly X509Certificate2 _cert;

    public ClientCertificateProviderTests()
    {
        Directory.CreateDirectory(_dir);
        _vault.Arrange(VaultState.Unlocked);
        _provider = new(_store, _vault);
        var request = new CertificateRequest("CN=test-client", _rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        _cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(30));
    }

    public void Dispose()
    {
        _provider.Dispose();
        _cert.Dispose();
        _rsa.Dispose();
        Directory.Delete(_dir, recursive: true);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private string Write(string name, byte[] bytes)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }

    private string Write(string name, string text)
    {
        var path = Path.Combine(_dir, name);
        File.WriteAllText(path, text);
        return path;
    }

    private async Task<ClientCertificateLookup> FindAsync(ClientCertificate entry)
    {
        await _store.SaveAsync([entry], Ct);
        return await _provider.FindAsync(Url, Ct);
    }

    private static ClientCertificate Entry(ClientCertificateFormat format, string path, string keyPath = "", bool pass = false) =>
        new(Guid.NewGuid(), "Corp", "api.example.com", format, path, keyPath, pass);

    private void SeedPassphrase(ClientCertificate entry) => _vault.Seed(SecretRef.Certificate(entry.Id), Secret);

    private static void AssertLoaded(ClientCertificateLookup lookup, ClientCertificate entry)
    {
        Assert.Null(lookup.Error);
        Assert.Equal(entry.Id.ToString(), lookup.CacheKey);
        Assert.NotNull(lookup.Certificate);
        Assert.True(lookup.Certificate.HasPrivateKey);
        Assert.Equal("CN=test-client", lookup.Certificate.Subject);
    }

    [Fact]
    public async Task Pfx_with_passphrase_from_the_vault_loads()
    {
        var entry = Entry(ClientCertificateFormat.Pfx, Write("a.pfx", _cert.Export(X509ContentType.Pfx, Secret)), pass: true);
        SeedPassphrase(entry);

        AssertLoaded(await FindAsync(entry), entry);
    }

    [Fact]
    public async Task Pfx_without_passphrase_loads()
    {
        var entry = Entry(ClientCertificateFormat.Pfx, Write("a.pfx", _cert.Export(X509ContentType.Pfx)));

        AssertLoaded(await FindAsync(entry), entry);
    }

    [Fact]
    public async Task Pem_with_a_separate_key_loads()
    {
        var entry = Entry(ClientCertificateFormat.Pem, Write("c.pem", _cert.ExportCertificatePem()), Write("k.pem", _rsa.ExportPkcs8PrivateKeyPem()));

        AssertLoaded(await FindAsync(entry), entry);
    }

    [Fact]
    public async Task Pem_combined_in_one_file_loads()
    {
        var entry = Entry(ClientCertificateFormat.Pem, Write("both.pem", _cert.ExportCertificatePem() + "\n" + _rsa.ExportPkcs8PrivateKeyPem()));

        AssertLoaded(await FindAsync(entry), entry);
    }

    [Fact]
    public async Task Encrypted_pem_key_loads_with_the_vault_passphrase()
    {
        var key = _rsa.ExportEncryptedPkcs8PrivateKeyPem(Secret, new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 1000));
        var entry = Entry(ClientCertificateFormat.Pem, Write("c.pem", _cert.ExportCertificatePem()), Write("k.pem", key), pass: true);
        SeedPassphrase(entry);

        AssertLoaded(await FindAsync(entry), entry);
    }

    [Fact]
    public async Task Wrong_passphrase_gives_an_error_without_the_secret()
    {
        var entry = Entry(ClientCertificateFormat.Pfx, Write("a.pfx", _cert.Export(X509ContentType.Pfx, Secret)), pass: true);
        _vault.Seed(SecretRef.Certificate(entry.Id), "wrong-one");

        var lookup = await FindAsync(entry);

        Assert.Null(lookup.Certificate);
        Assert.Equal(entry.Id.ToString(), lookup.CacheKey);
        Assert.NotNull(lookup.Error);
        Assert.DoesNotContain("wrong-one", lookup.Error);
        Assert.DoesNotContain(Secret, lookup.Error);
    }

    [Fact]
    public async Task Locked_vault_gives_an_error()
    {
        var entry = Entry(ClientCertificateFormat.Pfx, Write("a.pfx", _cert.Export(X509ContentType.Pfx, Secret)), pass: true);
        SeedPassphrase(entry);
        _vault.Arrange(VaultState.Locked);

        var lookup = await FindAsync(entry);

        Assert.Null(lookup.Certificate);
        Assert.Contains("Corp", lookup.Error);
    }

    [Fact]
    public async Task Missing_passphrase_secret_gives_an_error()
    {
        var entry = Entry(ClientCertificateFormat.Pfx, Write("a.pfx", _cert.Export(X509ContentType.Pfx, Secret)), pass: true);

        var lookup = await FindAsync(entry);

        Assert.Null(lookup.Certificate);
        Assert.Contains("Corp", lookup.Error);
    }

    [Fact]
    public async Task Missing_file_names_the_path()
    {
        var path = Path.Combine(_dir, "nope.pfx");

        var lookup = await FindAsync(Entry(ClientCertificateFormat.Pfx, path));

        Assert.Null(lookup.Certificate);
        Assert.Contains(path, lookup.Error);
    }

    [Fact]
    public async Task Certificate_without_a_private_key_gives_an_error()
    {
        using var publicOnly = X509CertificateLoader.LoadCertificate(_cert.RawData);
        var entry = Entry(ClientCertificateFormat.Pfx, Write("pub.pfx", publicOnly.Export(X509ContentType.Pfx)));

        var lookup = await FindAsync(entry);

        Assert.Null(lookup.Certificate);
        Assert.NotNull(lookup.Error);
    }

    [Fact]
    public async Task No_match_gives_an_empty_lookup()
    {
        var entry = Entry(ClientCertificateFormat.Pfx, Write("a.pfx", _cert.Export(X509ContentType.Pfx)));
        await _store.SaveAsync([entry], Ct);

        var lookup = await _provider.FindAsync(new Uri("https://other.example.org/"), Ct);

        Assert.Null(lookup.CacheKey);
        Assert.Null(lookup.Certificate);
        Assert.Null(lookup.Error);
    }

    [Fact]
    public async Task Cache_hit_returns_the_same_instance()
    {
        var entry = Entry(ClientCertificateFormat.Pfx, Write("a.pfx", _cert.Export(X509ContentType.Pfx)));

        var first = await FindAsync(entry);
        var second = await _provider.FindAsync(Url, Ct);

        Assert.Same(first.Certificate, second.Certificate);
    }

    [Fact]
    public async Task Failures_are_not_cached()
    {
        var path = Path.Combine(_dir, "late.pfx");
        var entry = Entry(ClientCertificateFormat.Pfx, path);
        Assert.NotNull((await FindAsync(entry)).Error);

        File.WriteAllBytes(path, _cert.Export(X509ContentType.Pfx));

        AssertLoaded(await _provider.FindAsync(Url, Ct), entry);
    }

    [Fact]
    public async Task Change_invalidates_and_disposes_the_cache()
    {
        var entry = Entry(ClientCertificateFormat.Pfx, Write("a.pfx", _cert.Export(X509ContentType.Pfx)));
        var first = await FindAsync(entry);

        await _store.SaveAsync([entry], Ct);
        var second = await _provider.FindAsync(Url, Ct);

        Assert.NotSame(first.Certificate, second.Certificate);
        Assert.Equal(IntPtr.Zero, first.Certificate!.Handle);
        AssertLoaded(second, entry);
    }

    [Fact]
    public async Task Dispose_disposes_cached_certificates()
    {
        var entry = Entry(ClientCertificateFormat.Pfx, Write("a.pfx", _cert.Export(X509ContentType.Pfx)));
        var lookup = await FindAsync(entry);

        _provider.Dispose();

        Assert.Equal(IntPtr.Zero, lookup.Certificate!.Handle);
    }
}
