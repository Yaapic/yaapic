# ADR 0014: Transport, client certificates, timings and HTTP version

Status: Accepted

See also: [ADR 0004](0004-http3-and-sdk.md), [ADR 0010](0010-secret-vault.md),
[spike M4-02](../spikes/m4-timings.md). The mTLS, timings and HTTP version cards of milestone 4 read this ADR first.

## Context

Milestone 4 adds mutual TLS (client certificates), a per-phase timing breakdown of each response and a per-request HTTP
version preference. All three touch `HttpClientTransport`, so they share one design. The spike measured what .NET can
report; its limits are recorded below.

## Decision

### Client certificates (mTLS)

- A global, per-user list stored outside the workspace in `<user data dir>/Yaapic/certificates.json` (same base folder
  rule as the vault of ADR 0010). Paths are machine specific, which is why the list is not in the workspace.
- Entry: `Id` (GUID), `Name`, `HostPattern`, `Format` (`Pfx` or `Pem`), `CertificatePath`, `KeyPath` (PEM only; empty when
  the PEM file also holds the private key), `HasPassphrase`.
- **Passphrase.** Stored in the vault under a new `SecretOwnerKind.Certificate`, owner = the entry id, name `passphrase`.
  A locked vault makes a send to a host that needs that certificate fail with `SendFailureKind.ClientCertificateError`
  ("The vault is locked, unlock it to use the certificate <name>"); sends to other hosts are unaffected.
- **Matching.** `https` only. `HostPattern` is `host`, `*.domain` (any depth of subdomain, not the bare domain) or either
  with `:port`; case-insensitive. Most specific wins: exact host and port, then exact host, then wildcard with port, then
  wildcard; ties go to the first entry of the list. A request without a matching entry sends no certificate.
- **Loading.** PFX through `X509CertificateLoader.LoadPkcs12FromFile` with the passphrase. PEM through
  `X509Certificate2.CreateFromPemFile` (certificate and key file, or one file), then re-imported from its PKCS#12 export
  so SChannel on Windows can use the private key. A bad passphrase, a missing file or a certificate without a private key
  gives `ClientCertificateError` with a user-presentable message. The loaded certificate is cached per entry id and
  disposed when the list changes.
- **Transport.** `HttpClientTransport` keeps its default clients and creates one extra `HttpClient` (plus its no-redirect
  twin) per matching certificate, cached by entry id, with `SslOptions.ClientCertificates` set. Server validation stays
  the system default; a custom CA or "skip verification" is the blocked card M4-37.

### Timings

`RequestTimings(TimeSpan? Dns, TimeSpan? Connect, TimeSpan? Tls, TimeSpan Wait, TimeSpan Download, bool ConnectionReused)`
is exposed as `HttpResponseData.Timings` (init property, `null` for responses built elsewhere). `Duration` stays the
total. The timings are not stored in the history.

Measurement, as proven by the spike:

| Phase | Source |
| --- | --- |
| `Dns` | around `Dns.GetHostAddressesAsync` in `SocketsHttpHandler.ConnectCallback` |
| `Connect` | around `Socket.ConnectAsync` in the same callback |
| `Tls` | end of connect to `SocketsHttpHandler.PlaintextStreamFilter`; `null` for plain `http` |
| `Wait` | headers received minus the end of the last connection phase (minus send start when reused) |
| `Download` | headers received to body read; the request is sent with `ResponseHeadersRead` |

- **Correction to the first design.** `RemoteCertificateValidationCallback` cannot be used for the end of TLS: its
  `sender` is the `SslStream`, never the request. `PlaintextStreamFilter` is used instead (it receives
  `InitialRequestMessage`). The measured TLS value is an approximation (connect end to filter, 307-339 ms against a
  300 ms reference delay).
- **Collector link.** A per-request collector is stored in `HttpRequestMessage.Options` with a
  `HttpRequestOptionsKey<T>`; both callbacks reach it through `InitialRequestMessage`. One request, one collector.
- **Reused connection.** Neither callback fires: `Dns`, `Connect` and `Tls` are `null`, `ConnectionReused` is true, and the
  UI shows "reused" (not 0 ms). Identical for HTTP/1.1 keep-alive and HTTP/2 multiplexing.
- **Redirects.** The request message is the same for every hop. The collector records one entry per new connection and the
  last hop's phases are reported, with the total elapsed time and an "N redirects" note; summing hops would mix unrelated
  hosts.
- **Limits.** A custom `ConnectCallback` replaces the handler's own connect logic: no proxy support for that connection and
  no dual-stack racing. The timing handler is therefore used only when no proxy applies to the URL (resolved through
  `HttpClient.DefaultProxy`); behind a proxy `Dns`, `Connect` and `Tls` are `null` and shown as "n/a", while `Wait` and
  `Download` are still measured. `PlaintextStreamFilter` also runs for plain `http`, so `Tls` stays `null` there. The cost
  is negligible (0.076 ms/request plain vs 0.068 instrumented over 200 pooled requests).
- **Fallback.** If the custom `ConnectCallback` proves unacceptable, report only `Wait` and `Download`.
- **Native AOT.** The spike project builds with `PublishAot=true` with 0 IL2xxx/IL3xxx warnings, but the published binary
  was **not run** (no C++ workload on the spike machine). Each implementation card must run the AOT publish and smoke test.

### HTTP version (confirms ADR 0004)

A per-request setting `HttpVersionPreference`: `Default` (HTTP/1.1 with ALPN upgrade to HTTP/2), `Http1`, `Http2`, `Http3`.
`Http3` uses `HttpVersionPolicy.RequestVersionOrLower`, is disabled in the UI when `QuicConnection.IsSupported` is false,
and falls back to a lower version when the server does not speak HTTP/3. The protocol actually used is shown in the
response (`HttpResponseData.Protocol`, for example `HTTP/2`). The file key is `settings.httpVersion` (card M4-38).

### Native AOT

`SocketsHttpHandler`, `SslClientAuthenticationOptions`, `X509CertificateLoader` and `QuicConnection.IsSupported` involve no
reflection; verified by the AOT publish of the apps in the implementation cards.

## Out of scope

Proxy settings, custom CA bundles, OS certificate stores, HSM or smart cards, SSL key logging.
