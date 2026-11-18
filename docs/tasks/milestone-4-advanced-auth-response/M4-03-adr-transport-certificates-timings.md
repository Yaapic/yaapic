# M4-03: ADR 0014: transport, client certificates, timings and HTTP version

Status: done
Platform: any
Depends on: M4-02

## Goal

Write ADR 0014, the reference for the mTLS, timings and HTTP version cards. Docs only. The decisions below are final unless the spike
(`docs/spikes/m4-timings.md`) proved a measurement impossible, in which case the ADR records the fallback the spike proposed.

## Design decisions (to record in the ADR)

- **Client certificates (mTLS).** A global, per-user list stored outside the workspace in `<user data dir>/Yaapic/certificates.json`
  (same base folder rule as the vault in ADR 0010). Entry: `Id` (GUID), `Name`, `HostPattern`, `Format` (`Pfx` or `Pem`),
  `CertificatePath`, `KeyPath` (PEM only; empty when the PEM file also holds the private key), `HasPassphrase`. Paths are machine specific,
  which is why the list is not in the workspace.
- **Passphrase.** Stored in the vault (ADR 0010) under a new `SecretOwnerKind.Certificate` with the entry id as owner and the name
  `passphrase`. A locked vault makes a send to a host that needs that certificate fail with `SendFailureKind.ClientCertificateError`
  ("The vault is locked, unlock it to use the certificate <name>"); sends to other hosts are unaffected.
- **Matching.** `https` only. `HostPattern` is `host`, `*.domain` (any depth of subdomain, not the bare domain) or either with `:port`;
  case-insensitive. Most specific wins: exact host and port, then exact host, then wildcard with port, then wildcard; ties go to the first
  entry of the list. A request without a matching entry sends no certificate.
- **Loading.** PFX through `X509CertificateLoader.LoadPkcs12FromFile` with the passphrase. PEM through `X509Certificate2.CreateFromPemFile`
  (certificate and key file, or one file), then re-imported from its PKCS#12 export so the private key is usable by SChannel on Windows.
  A bad passphrase, a missing file or a certificate without a private key gives `ClientCertificateError` with a user-presentable message.
  The loaded certificate is cached per entry id and disposed when the list changes.
- **Transport.** `HttpClientTransport` keeps its default clients and creates one extra `HttpClient` (plus its no-redirect twin) per
  matching certificate, cached by entry id, with `SslOptions.ClientCertificates` set. Server validation stays the system default; a custom
  CA or "skip verification" is the blocked card M4-37.
- **Timings.** `RequestTimings(TimeSpan? Dns, TimeSpan? Connect, TimeSpan? Tls, TimeSpan Wait, TimeSpan Download, bool ConnectionReused)`
  on `HttpResponseData.Timings` (init property, `null` for responses built elsewhere). `Duration` stays the total. Measurement per the
  spike (expected: `ConnectCallback` for DNS and TCP connect, the server certificate validation callback for the end of TLS, headers
  received for `Wait`, body read for `Download`). A phase that cannot be measured is `null` and shown as "n/a"; a reused connection has
  `Dns`, `Connect` and `Tls` equal to `null` and `ConnectionReused` true. Redirects report the last hop (or what the spike decided). The
  timings are not stored in the history.
- **HTTP version** (confirms ADR 0004): a per-request setting `HttpVersionPreference` `Default` (HTTP/1.1 with ALPN upgrade to HTTP/2),
  `Http1`, `Http2`, `Http3`. `Http3` uses `HttpVersionPolicy.RequestVersionOrLower`, is disabled in the UI when `QuicConnection.IsSupported`
  is false, and falls back to a lower version when the server does not speak HTTP/3. The protocol actually used is shown in the response
  (`HttpResponseData.Protocol`, for example `HTTP/2`).
- **Native AOT.** `SocketsHttpHandler`, `SslClientAuthenticationOptions`, `X509CertificateLoader`, `QuicConnection.IsSupported`: no
  reflection; verified by the AOT publish of the apps in the cards.

## Out of scope

Code, UI, proxy settings, custom CA bundles, certificate stores of the OS, HSM or smart cards, SSL key logging.

## Acceptance criteria

- [x] ADR 0014 (`docs/adr/0014-transport-certificates-timings.md`) contains every decision above, the spike's measured limits, and
  updates ADR 0004's status line ("HTTP/3 scope confirmed in M4"); `docs/adr/README.md` lists it.
- [x] `docs/file-format.md` documents `settings.httpVersion` (shape defined by M4-38) and says certificates are not in the workspace.
- [x] No code changed; the build still has 0 warnings.

## Suggested commit message

`docs(adr): add ADR 0014 transport, client certificates and timings`
