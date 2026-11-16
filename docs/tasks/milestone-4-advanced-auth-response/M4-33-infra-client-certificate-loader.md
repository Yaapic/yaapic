# M4-33: Infrastructure: client certificate loader and provider

Status: todo
Platform: any
Depends on: M4-30, M4-31, M4-32

## Goal

The real `IClientCertificateProvider`: finds the matching entry, loads its certificate (PFX or PEM) with its passphrase from the vault, and caches it.

## Design decisions

- Read first: ADR 0014 (Loading, Passphrase), M4-30 to M4-32, `ISecretVault`.
- `src/Yaapic.Infrastructure/Certificates/ClientCertificateProvider.cs`: `public sealed class ClientCertificateProvider(ICertificateStore store,
  ISecretVault vault) : IClientCertificateProvider, IDisposable`.
- `FindAsync(url)`: load the list (cached in memory; the cache is invalidated by `store.Changed`), `ClientCertificateMatcher.Find`; no match gives
  `new(null, null, null)`; a match gives `CacheKey` = id and a `Certificate` from the cache or loaded now.
- Passphrase: `HasPassphrase` true needs `vault.TryGet(SecretRef.Certificate(id), out value)`; a locked vault gives the error
  `Strings.CertificateVaultLocked(name)`; a missing secret gives `Strings.CertificatePassphraseMissing(name)`. `HasPassphrase` false uses no
  passphrase.
- Loading:
  - `Pfx`: `X509CertificateLoader.LoadPkcs12FromFile(path, passphrase, X509KeyStorageFlags.EphemeralKeySet)` (on macOS and Linux
    `EphemeralKeySet` is fine; on Windows ephemeral keys cannot be used for TLS client auth with SChannel: use `X509KeyStorageFlags.DefaultKeySet`
    on Windows, which is what the ADR requires verifying; record the flag you needed in the Notes).
  - `Pem`: `X509Certificate2.CreateFromPemFile(certPath, keyPathOrNull)` (or `CreateFromEncryptedPemFile` when a passphrase is set), then
    `X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null)` so the key is usable on Windows; the original is disposed.
  - A missing file, a bad passphrase (`CryptographicException`) or a certificate without a private key gives an error string
    (`Strings.CertificateFileMissing(path)`, `CertificateLoadFailed(name)`, `CertificateNoPrivateKey(name)`) that never contains the passphrase
    or key text. Failures are not cached (the next send retries).
  - The certificate is also checked for validity dates only informatively: an expired certificate still loads (the server decides) and the
    provider does not warn in M4.
- Cached certificates are disposed when the list changes and in `Dispose`.
- Strings listed above, EN and FR.

## Out of scope

Using the certificate in the transport (M4-34), UI.

## Acceptance criteria

- [ ] Tests in `Yaapic.Infrastructure.Tests` with certificates generated at test time (`CertificateRequest`, self-signed, exported to PFX with
  and without passphrase and to PEM cert + key files in a temp folder), `InMemoryCertificateStore` and `InMemorySecretVault`: PFX with
  passphrase from the vault; PFX without; PEM with separate key; PEM combined in one file; encrypted PEM key; wrong passphrase; locked
  vault; missing passphrase secret; missing file; certificate with no private key; no match returns an empty lookup; cache hit loads once;
  `Changed` invalidates; errors contain no secret.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(infra): load client certificates from PFX and PEM files`
