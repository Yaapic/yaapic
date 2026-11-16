using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Yaapic.Core.Certificates;
using Yaapic.Core.Secrets;
using Yaapic.Core.Storage;
using Yaapic.Domain.Http;
using Yaapic.Localization;

namespace Yaapic.Infrastructure.Certificates;

/// <summary>Finds the client certificate entry for a URL and loads it from its PFX or PEM files, caching the result.</summary>
/// <param name="store">The list of configured certificates.</param>
/// <param name="vault">The vault holding the certificate passphrases.</param>
public sealed class ClientCertificateProvider(ICertificateStore store, ISecretVault vault) : IClientCertificateProvider, IDisposable
{
    // Windows SChannel cannot use ephemeral keys for TLS client authentication.
    private static readonly X509KeyStorageFlags PfxFlags =
        OperatingSystem.IsWindows() ? X509KeyStorageFlags.DefaultKeySet : X509KeyStorageFlags.EphemeralKeySet;

    private readonly Lock _gate = new();
    private readonly Dictionary<Guid, X509Certificate2> _certificates = [];
    private IReadOnlyList<ClientCertificate>? _entries;
    private int _version;
    private bool _subscribed;

    /// <inheritdoc/>
    public async Task<ClientCertificateLookup> FindAsync(Uri url, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(url);
        Subscribe();

        int version;
        IReadOnlyList<ClientCertificate>? entries;
        lock (_gate)
        {
            version = _version;
            entries = _entries;
        }

        if (entries is null)
        {
            entries = await store.LoadAsync(ct).ConfigureAwait(false);
            lock (_gate)
            {
                // A change during the load makes the list stale: use it once, do not cache it.
                if (version == _version) _entries = entries;
            }
        }

        var entry = ClientCertificateMatcher.Find(entries, url);
        if (entry is null) return new(null, null, null);

        var key = entry.Id.ToString();
        lock (_gate)
        {
            if (_certificates.TryGetValue(entry.Id, out var cached)) return new(key, cached, null);
        }

        var (certificate, error) = Load(entry);
        if (certificate is null) return new(key, null, error);

        lock (_gate)
        {
            if (_certificates.TryGetValue(entry.Id, out var raced))
            {
                certificate.Dispose();
                return new(key, raced, null);
            }

            _certificates[entry.Id] = certificate;
        }

        return new(key, certificate, null);
    }

    /// <summary>Unsubscribes from the store and disposes the cached certificates.</summary>
    public void Dispose()
    {
        if (_subscribed) store.Changed -= OnChanged;
        Reset();
    }

    private void Subscribe()
    {
        lock (_gate)
        {
            if (_subscribed) return;
            _subscribed = true;
        }

        store.Changed += OnChanged;
    }

    private void OnChanged(object? sender, EventArgs e) => Reset();

    private void Reset()
    {
        lock (_gate)
        {
            foreach (var certificate in _certificates.Values) certificate.Dispose();
            _certificates.Clear();
            _entries = null;
            _version++;
        }
    }

    private (X509Certificate2? Certificate, string? Error) Load(ClientCertificate entry)
    {
        string? passphrase = null;
        if (entry.HasPassphrase)
        {
            if (vault.State != VaultState.Unlocked) return (null, Strings.CertificateVaultLocked(entry.Name));
            if (!vault.TryGet(SecretRef.Certificate(entry.Id), out var value)) return (null, Strings.CertificatePassphraseMissing(entry.Name));
            passphrase = value;
        }

        var hasKeyFile = entry.Format == ClientCertificateFormat.Pem && !string.IsNullOrEmpty(entry.KeyPath);
        if (!File.Exists(entry.CertificatePath)) return (null, Strings.CertificateFileMissing(entry.CertificatePath));
        if (hasKeyFile && !File.Exists(entry.KeyPath)) return (null, Strings.CertificateFileMissing(entry.KeyPath));

        try
        {
            var certificate = entry.Format == ClientCertificateFormat.Pfx
                ? X509CertificateLoader.LoadPkcs12FromFile(entry.CertificatePath, passphrase, PfxFlags)
                : LoadPem(entry, hasKeyFile, passphrase);
            if (certificate.HasPrivateKey) return (certificate, null);

            certificate.Dispose();
            return (null, Strings.CertificateNoPrivateKey(entry.Name));
        }
        catch (Exception e) when (e is CryptographicException or IOException or UnauthorizedAccessException or ArgumentException)
        {
            // The message is dropped on purpose: it could echo key material.
            return (null, Strings.CertificateLoadFailed(entry.Name));
        }
    }

    private static X509Certificate2 LoadPem(ClientCertificate entry, bool hasKeyFile, string? passphrase)
    {
        var keyPath = hasKeyFile ? entry.KeyPath : null;
        using var pem = passphrase is null
            ? X509Certificate2.CreateFromPemFile(entry.CertificatePath, keyPath)
            : X509Certificate2.CreateFromEncryptedPemFile(entry.CertificatePath, passphrase, keyPath);
        // PEM keys are not usable for TLS on Windows until re-imported from PKCS#12.
        return pem.HasPrivateKey
            ? X509CertificateLoader.LoadPkcs12(pem.Export(X509ContentType.Pfx), null, PfxFlags)
            : new X509Certificate2(pem);
    }
}
