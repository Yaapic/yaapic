# Spike M4-02: request phases .NET can report

Throwaway console project (not committed), .NET SDK 11.0.100, Windows 11 (10.0.26300), `net11.0`, C# 15. Local server: dual-stack
`TcpListener` + `SslStream` with a self-signed certificate and a **300 ms** sleep before the TLS handshake, HTTP/1.1 keep-alive.
Public host: `https://example.com`. Numbers are one run, in milliseconds; they show orders of magnitude, not benchmarks.

## Answers

| # | Question | Outcome |
| --- | --- | --- |
| 1 | `ConnectCallback` returning a `NetworkStream` | Works. The handler still does TLS on the returned stream for `https` URLs and negotiates ALPN (`HTTP/1.1` and `HTTP/2.0` both observed on example.com). DNS and TCP connect are timed inside the callback (local: dns 7, tcp 1.6; example.com: dns 1, tcp 12-15). Note: connecting to `localhost` on a listener bound to IPv4 only costs ~2 s on Windows (the `::1` attempt is retried); the spike server is dual-stack to avoid that artefact. |
| 2 | Link connection to request | `ctx.InitialRequestMessage.Options` returns the exact `HttpRequestMessage` the caller sent, so a collector stored with `HttpRequestOptionsKey<T>` is reachable from `ConnectCallback`. Confirmed on every new connection. |
| 3 | TLS via `RemoteCertificateValidationCallback` | **Not usable as proposed:** with `SslOptions` the `sender` is the `SslStream`, never the request message, so the callback cannot reach the collector. **Use `SocketsHttpHandler.PlaintextStreamFilter` instead**: it runs once per new connection right after the handshake, receives a context with `InitialRequestMessage` (collector reachable), `NegotiatedHttpVersion` and the `PlaintextStream`. Time from connect end to the filter: **307-339 ms** against the 300 ms reference delay (extra ~7-39 ms is the real ClientHello/cert/Finished round trips plus first-use JIT); example.com: 16-27 ms. A good approximation of "TLS handshake". |
| 4 | TTFB and download | `SendAsync(..., ResponseHeadersRead)` returns at headers; reading the body afterwards gives the download. Both measurable with a `Stopwatch`. Caveat: the headers timestamp includes DNS, TCP and TLS on a new connection, so **waiting (TTFB) = headers - max(connect end, TLS end)**, otherwise connection time is counted twice. |
| 5 | Reused connection | Neither `ConnectCallback` nor `PlaintextStreamFilter` fires (`Connections == 0`); show the phases as "reused", not 0 ms. HTTP/2 multiplexing identical: second and two parallel requests on one `HttpClient` created no connection. HTTP/1.1 keep-alive same. |
| 6 | Redirects | The `HttpRequestMessage` instance is the same for every hop, so the collector is shared. Same host: the connection is reused (1 connection, phases = first hop). Other host: callbacks fire again (2 connections). **Decision: record phases per connection event, report the last hop's phases and the total elapsed time**, with a "N redirects" note; summing mixes unrelated hosts and would be misleading. The collector must therefore keep a list of connection events (or overwrite per hop) and the UI picks the last. |
| 7 | Native AOT | **Not verified.** `dotnet publish -r win-x64 -p:PublishAot=true` stops at "Platform linker not found": the Visual Studio 18 installation here lacks the *Desktop development with C++* workload, and installing it is a system change outside this card. The project compiles with `PublishAot=true` (which turns on the AOT and trim analyzers) and `TreatWarningsAsErrors`: **0 IL2xxx/IL3xxx warnings at build**. The APIs used (`SocketsHttpHandler`, `Socket`, `Dns`, `SslStream`, `HttpRequestOptionsKey<T>`) involve no reflection. The runtime check of the published binary is **still to do** on a machine with the C++ workload (M4 implementation cards must run it anyway). |
| 8 | Cost | 200 pooled requests: 0.076 ms/request plain vs 0.068 ms/request instrumented. Negligible (the callbacks do not run on pooled requests; the only per-request cost is an `Options` entry). |

## Phases that are measurable

| Phase | Source | Reused connection |
| --- | --- | --- |
| DNS | around `Dns.GetHostAddressesAsync` in `ConnectCallback` | n/a ("reused") |
| TCP connect | around `Socket.ConnectAsync` in `ConnectCallback` | n/a ("reused") |
| TLS handshake | connect end to `PlaintextStreamFilter`; null for plain `http` | n/a ("reused") |
| Waiting (TTFB) | headers read minus the last connection phase end (or minus send start when reused) | measured |
| Download | headers read to body read | measured |

Fallback if a custom `ConnectCallback` is not acceptable (it bypasses proxy support and `Happy Eyeballs`, see risks): report only
waiting and download, with DNS/TCP/TLS as "n/a".

## Key snippets that worked

```csharp
static readonly HttpRequestOptionsKey<Collector> Key = new("timings");

var handler = new SocketsHttpHandler
{
    ConnectCallback = async (ctx, ct) =>
    {
        ctx.InitialRequestMessage.Options.TryGetValue(Key, out var c);
        long t0 = Stopwatch.GetTimestamp();
        var addresses = await Dns.GetHostAddressesAsync(ctx.DnsEndPoint.Host, ct);
        long t1 = Stopwatch.GetTimestamp();
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try { await socket.ConnectAsync(addresses, ctx.DnsEndPoint.Port, ct); }
        catch { socket.Dispose(); throw; }
        c?.AddConnection(t0, t1, Stopwatch.GetTimestamp());
        return new NetworkStream(socket, ownsSocket: true);
    },
    PlaintextStreamFilter = (ctx, ct) =>
    {
        if (ctx.InitialRequestMessage.Options.TryGetValue(Key, out var c))
            c.TlsEnded(Stopwatch.GetTimestamp(), ctx.NegotiatedHttpVersion);
        return ValueTask.FromResult(ctx.PlaintextStream);
    },
};
// request: req.Options.Set(Key, collector); SendAsync(req, HttpCompletionOption.ResponseHeadersRead)
```

## Risks for ADR 0014

- A custom `ConnectCallback` replaces the handler's own connect logic: **proxy support is lost for that connection** (the callback
  connects directly to the origin) and there is no dual-stack racing. The ADR must either apply the timing handler only when no proxy
  is configured, or accept "n/a" phases behind a proxy.
- `PlaintextStreamFilter` is also called for plain `http` connections (no TLS): the TLS phase must stay null there.
- Collector writes happen on connection threads: use plain field writes guarded by the one-request-one-collector rule, or
  `Interlocked`/a lock if a collector ever serves parallel requests.
