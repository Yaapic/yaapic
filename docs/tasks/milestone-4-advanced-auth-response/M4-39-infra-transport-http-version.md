# M4-39: Infrastructure: send with the chosen HTTP version

Status: done
Platform: any
Depends on: M4-36, M4-38

## Goal

`HttpClientTransport` sends with the requested HTTP version and reports the protocol actually used.

## Design decisions

- Read first: ADR 0014 (HTTP version), ADR 0004, `RequestResolver.cs` (how `FollowRedirects` and `TimeoutSeconds` reach `ResolvedRequest`),
  `HttpClientTransport.cs`.
- `ResolvedRequest` gains a last optional parameter `HttpVersionPreference HttpVersion = HttpVersionPreference.Default`; the resolver copies it
  from `request.Settings.HttpVersion`.
- Core port `IHttpVersionSupport { bool IsHttp3Supported { get; } }` in `Yaapic.Core.Http`; `QuicHttpVersionSupport` in Infrastructure returns
  `System.Net.Quic.QuicConnection.IsSupported` (guarded by `[SupportedOSPlatformGuard]` as the analyzers require; the property is evaluated
  once, lazily, inside a try/catch returning false). A test double `FakeHttpVersionSupport`.
- Transport mapping on the `HttpRequestMessage`:
  - `Default`: `Version = 1.1`, `VersionPolicy = RequestVersionOrHigher` (so ALPN may upgrade to HTTP/2, which is the `SocketsHttpHandler`
    default behaviour; verify against a local HTTP/2 server and state the result).
  - `Http1`: `Version = 1.1`, `VersionPolicy = RequestVersionExact`.
  - `Http2`: `Version = 2.0`, `VersionPolicy = RequestVersionOrLower`.
  - `Http3`: `Version = 3.0`, `VersionPolicy = RequestVersionOrLower`; when `IsHttp3Supported` is false the request is sent as `Http2` and the
    response `Protocol` is whatever was used (the UI already disables the choice, M4-59, so this is a safety net, not a feature).
  - The handler's `EnableMultipleHttp2Connections` stays at its default.
- After the response, `Protocol` is `"HTTP/" + response.Version` trimmed to `HTTP/1.1`, `HTTP/2`, `HTTP/3` (`2.0` becomes `2`, `3.0` becomes `3`).
  It is set through the init property of M4-35 next to `Timings`.
- `HttpClientTransport` receives `IHttpVersionSupport? support = null` (null means `false`) as an optional last constructor parameter.
- HTTP/3 needs the msquic library: nothing is bundled; an environment without it simply has `IsHttp3Supported` false.

## Out of scope

The settings UI (M4-59, M4-69, M4-77), showing the protocol (UI timing and status cards), bundling msquic.

## Acceptance criteria

- [x] Tests in `Yaapic.Infrastructure.Tests`: a recording `HttpMessageHandler`-level test cannot see the version, so use a local TLS server with
  ALPN (`SslStream` server offering `h2` and `http/1.1` is heavy: instead assert the mapping through an internal pure function
  `HttpVersionMapping.Apply(HttpRequestMessage, HttpVersionPreference, bool http3Supported)` tested for all four values and both support
  states) plus one end-to-end test with the local plain-HTTP server proving `Http1` and `Default` report `HTTP/1.1`; `Protocol` text mapping
  for 1.1, 2.0, 3.0.
- [x] Core test: the resolver copies `HttpVersion`.
- [x] Existing transport tests unchanged; Native AOT publish of the host's app project has 0 IL2xxx/IL3xxx warnings and the exe starts and exits (kill
  it afterwards).
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(infra): send with the requested HTTP version and report the protocol`
