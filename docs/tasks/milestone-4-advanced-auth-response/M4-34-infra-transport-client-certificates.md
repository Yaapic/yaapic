# M4-34: Infrastructure: send with a client certificate (mTLS)

Status: done
Platform: any
Depends on: M4-33

## Goal

`HttpClientTransport` presents the matching client certificate during the TLS handshake.

## Design decisions

- Read first: ADR 0014 (Transport), `HttpClientTransport.cs`, `DefaultHttpClient.cs`, M4-30 and M4-33, `FakeHttpTransport`.
- `SendFailureKind` gains `ClientCertificateError` (appended; XML doc). Map it in any `switch` over the kind (history, UI text: grep
  `SendFailureKind`; unmapped places show the message as they do for `Unknown`).
- `DefaultHttpClient` gains `Create(X509Certificate2 certificate)` and `CreateNonRedirecting(X509Certificate2 certificate)` building the same
  handler plus `SslOptions = new SslClientAuthenticationOptions { ClientCertificates = [certificate] }`.
- `HttpClientTransport` gets an optional constructor parameter `IClientCertificateProvider? certificates = null` and an optional
  `Action<SocketsHttpHandler>? configureHandler = null` (tests only: lets a test accept a self-signed server certificate; documented as such;
  applied to every handler the transport creates, default ones included, so the composition passes `null`).
  Existing constructor call sites keep compiling (parameters added last, optional).
- In `SendAsync`, after the URL validation: when a provider is set, `FindAsync(uri)`; a lookup with an error returns
  `SendResult.Fail(SendFailureKind.ClientCertificateError, error)` without connecting; a lookup with a certificate picks (or creates and
  caches by `CacheKey`, in a `ConcurrentDictionary`) a pair of clients (follow and no-follow) built for it; no entry uses the default clients.
- The transport disposes the per-certificate clients in a new `Dispose` (it implements `IDisposable`; the composition disposes it on exit)
  and drops them when the provider reports a different certificate instance for the same key (check by reference).
- Everything else (timeout, redirects, bodies) is unchanged and shares one code path.

## Out of scope

Timings (M4-36), custom CA and skip verification (M4-37), UI.

## Acceptance criteria

- [x] Integration test in `Yaapic.Infrastructure.Tests/EndToEnd/` with a local TLS server (`TcpListener` + `SslStream.AuthenticateAsServer` with
  `clientCertificateRequired: true`, a generated self-signed server certificate accepted through `configureHandler`, a minimal HTTP/1.1
  answer that echoes the client certificate thumbprint in the body): a matching entry shows the thumbprint of the client certificate;
  a URL that does not match sends none (the server sees no client certificate); a lookup error fails with `ClientCertificateError` without a
  connection; two sends reuse the same cached client (one handshake per connection: assert by the cache count, not by timing).
- [x] Existing transport tests unchanged and green.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green; no listener or process left open.

## Suggested commit message

`feat(infra): send requests with a matching client certificate`
