# M4-02: Spike: which request phases .NET can report

Status: todo
Platform: any
Depends on: none

## Goal

Find out, with a throwaway program, which phases of a request `SocketsHttpHandler` lets us measure under Native AOT (DNS, TCP connect,
TLS handshake, time to first byte, download), and record the result so M4-03 can write ADR 0014 on facts.

## Design decisions

- Throwaway console project in the scratchpad or a temporary folder **outside the repository solution**; it is not committed. The only
  committed file is `docs/spikes/m4-timings.md` with the results and the key code snippets.
- Questions to answer, each with the measured outcome:
  1. `SocketsHttpHandler.ConnectCallback`: resolve with `Dns.GetHostAddressesAsync`, connect with a `Socket`, return a `NetworkStream`
     (`ownsSocket: true`). Does the handler still do TLS and HTTP/2 negotiation on the returned stream for an `https` URL?
  2. Linking a connection to a request: `SocketsHttpConnectionContext.InitialRequestMessage` and `HttpRequestMessage.Options` carry a
     collector object; confirm it is the request that caused the connection.
  3. TLS: `SslOptions.RemoteCertificateValidationCallback` (its `sender` is the request message) fires once per new TLS connection. Is
     "time between connect end and that callback" a usable approximation of the handshake? Compare with a reference: a local `SslStream`
     server that delays its handshake by a known amount.
  4. Time to first byte: from `SendAsync` start to `ResponseHeadersRead`; download: until the body is read.
  5. A reused pooled connection: the callbacks do not fire; the phases are reported as "reused", not zero. HTTP/2 multiplexing: same.
  6. Redirects: the phases of the last hop only, or the sum? Pick and justify.
  7. Native AOT: `dotnet publish -r <host rid> -p:PublishAot=true` of the spike has 0 IL2xxx/IL3xxx warnings and runs.
  8. Cost: overhead of the callbacks on a local request (expected negligible).
- Test against a local server (an `SslStream` + `TcpListener` server for TLS, a plain `TcpListener` for HTTP) and one public HTTPS host
  (`https://example.com`) for realism.
- If a phase is not measurable, say so and propose the fallback (a null phase shown as "n/a").

## Out of scope

Any production code, the ADR (M4-03), the UI.

## Acceptance criteria

- [ ] `docs/spikes/m4-timings.md` answers the eight questions with the numbers observed and the snippets that worked.
- [ ] The spike published as Native AOT with 0 warnings (stated in the document, with the SDK and OS used).
- [ ] No file of the solution changed; the temporary project is deleted; no process left running.

## Suggested commit message

`docs: record the request timing capture spike`
