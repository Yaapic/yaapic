# M4-36: Infrastructure: capture request timings in the transport

Status: todo
Platform: any
Depends on: M4-34, M4-35

## Goal

Every response from `HttpClientTransport` carries `RequestTimings`, measured the way the spike (`docs/spikes/m4-timings.md`) proved to work.

## Design decisions

- Read first: ADR 0014 (Timings), `docs/spikes/m4-timings.md` (the snippets there are the starting point and win over this card where they
  differ: record the difference in the Notes), `HttpClientTransport.cs`, `DefaultHttpClient.cs`, M4-35.
- `internal sealed class TimingCollector` in `Yaapic.Infrastructure.Http`: thread-safe fields for the stopwatch timestamps (DNS start and
  end, connect end, TLS end, headers received, body end) and a `bool ConnectionOpened`; `RequestTimings Build()` produces the record:
  `Dns` and `Connect` from the connect callback, `Tls` from the end of the connect to the TLS marker the spike chose, `Wait` = headers
  received minus request start minus the connection phases that happened, `Download` = body end minus headers received,
  `ConnectionReused` = no connect callback fired for this request. A phase the spike marked unmeasurable stays `null`.
- The collector is attached to each `HttpRequestMessage` through `message.Options.Set(TimingCollector.Key, collector)`.
  `DefaultHttpClient` handlers (all four builders, including the certificate ones of M4-34) get a `ConnectCallback` that resolves DNS
  and connects exactly as the spike did, reading the collector from `context.InitialRequestMessage.Options`, and the TLS marker the spike
  chose (`SslOptions.RemoteCertificateValidationCallback` that records the time and then returns the **default** validation result: use
  `SslPolicyErrors.None` as accept and anything else as reject, matching the system default; no behaviour change).
- `SendAsync` creates a collector per call, starts the stopwatch before `SendAsync` of the client, marks headers received after
  `ResponseHeadersRead`, marks the body end after `ReadAsByteArrayAsync`, and sets `Timings = collector.Build()` on the returned
  `HttpResponseData` (init property of M4-35). `Duration` is unchanged.
- Redirects: apply the spike's decision (last hop, or sum) and state it in the XML doc of `TimingCollector`.
- A failure result has no timings. A connection failure inside the callback keeps its current mapping to `ConnectionFailed` with the same
  message text as before (the existing transport tests must pass unchanged).
- Overhead: no allocation per phase beyond the collector; no logging.

## Out of scope

Display (M4-45, UI cards), storage in history, HTTP version (M4-39).

## Acceptance criteria

- [ ] Integration tests in `Yaapic.Infrastructure.Tests/EndToEnd/` against local servers: a plain HTTP server that delays the response headers
  by 200 ms and the body by 200 ms gives `Wait` >= 180 ms and `Download` >= 180 ms (generous lower bounds only, no upper bounds); a TLS server
  that delays its handshake by 300 ms gives `Tls` >= 250 ms; a second request on the same pooled connection has `ConnectionReused` true and
  null connection phases; a first request has a non-null `Connect` (and `Dns` per the spike); `Sum` <= `Duration`.
- [ ] Existing transport tests unchanged and green; the callbacks do not change TLS validation behaviour (a self-signed server without
  `configureHandler` still fails as before).
- [ ] Native AOT publish of the host's app project: 0 IL2xxx/IL3xxx warnings, the published exe starts and exits (kill it afterwards).
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(infra): measure DNS, connect, TLS, wait and download timings`
