# Spike M6-02: concurrent scripted sends under load

Program: [`docs/spikes/m6-load/`](m6-load/) (throwaway console project, not in `Yaapic.sln`, own empty
`Directory.Build.props`/`Directory.Packages.props`). It references `Yaapic.Core`, `Yaapic.Infrastructure` and `Yaapic.Scripting`
by path, starts a keep-alive `TcpListener` server on `127.0.0.1` (answers `200 {"ok":true}`), then runs N concurrent loops for
20 seconds, each calling `ScriptedRequestSender.SendAsync` on a GET through the shared `HttpClientTransport`.
Usage: `LoadSpike [seconds] [N,N,...]`.

Script kinds: **none** (no script), **js** (pre-request `yaapic.variables.set('t', Date.now());`, post-response
`yaapic.test('ok', () => yaapic.expect(yaapic.response.code).to.equal(200));`), **lua** (the same in Lua; Lua has no `os`
library in the sandbox). So each scripted send runs **two** script runs, hence two dedicated threads.

## Machine

13th Gen Intel Core i7-13700H, 20 logical cores, 63.8 GiB RAM, Windows 11 10.0.26300, .NET 11.0.0, same host also running the
server and the load generator.

## Results

Columns: requests per second, latency percentiles of one `SendAsync` (milliseconds), errors (failed send, failed test or failed
script), runs that reported `TimedOut`, peak working set (MiB) and peak thread count (sampled every 250 ms).

### JIT build (Release)

| kind | N | req/s | p50 | p95 | p99 | errors | timed out | peak WS | peak threads |
|------|---|------:|----:|----:|----:|-------:|----------:|--------:|-------------:|
| none | 10 | 99118 | 0.1 | 0.1 | 0.2 | 0 | 0 | 86 | 44 |
| none | 50 | 104676 | 0.3 | 1.1 | 2.8 | 0 | 0 | 147 | 51 |
| none | 100 | 101031 | 0.7 | 3.1 | 4.4 | 0 | 0 | 180 | 49 |
| none | 200 | 98334 | 1.5 | 4.7 | 6.1 | 0 | 0 | 185 | 46 |
| js | 10 | 924 | 10.6 | 13.1 | 17.0 | 0 | 0 | 205 | 55 |
| js | 50 | 1078 | 45.1 | 65.0 | 81.6 | 0 | 0 | 207 | 107 |
| js | 100 | 1125 | 86.9 | 107.7 | 123.0 | 0 | 0 | 190 | 104 |
| js | 200 | 1083 | 182.0 | 207.9 | 230.1 | 0 | 0 | 119 | 97 |
| lua | 10 | 1430 | 6.9 | 8.5 | 9.3 | 0 | 0 | 103 | 48 |
| lua | 50 | 1604 | 30.6 | 41.2 | 47.7 | 0 | 0 | 104 | 91 |
| lua | 100 | 1559 | 63.1 | 76.1 | 83.1 | 0 | 0 | 104 | 88 |
| lua | 200 | 1493 | 131.1 | 154.5 | 170.3 | 0 | 0 | 103 | 88 |

### Native AOT publish (`-r win-x64 -p:PublishAot=true`)

The publish ends with 0 warnings (no IL2xxx/IL3xxx) and the published `LoadSpike.exe` ran to the end and exited cleanly.

| kind | N | req/s | p50 | p95 | p99 | errors | timed out | peak WS | peak threads |
|------|---|------:|----:|----:|----:|-------:|----------:|--------:|-------------:|
| none | 10 | 108086 | 0.1 | 0.1 | 0.2 | 0 | 0 | 58 | 32 |
| none | 50 | 102997 | 0.4 | 0.8 | 2.6 | 0 | 0 | 132 | 70 |
| none | 100 | 98808 | 0.8 | 3.5 | 4.3 | 0 | 0 | 153 | 119 |
| none | 200 | 91958 | 1.9 | 6.4 | 7.7 | 0 | 0 | 182 | 215 |
| js | 10 | 908 | 10.7 | 13.9 | 17.5 | 0 | 0 | 210 | 229 |
| js | 50 | 1161 | 42.1 | 58.8 | 69.1 | 0 | 0 | 223 | 272 |
| js | 100 | 1136 | 88.2 | 114.3 | 127.0 | 0 | 0 | 150 | 300 |
| js | 200 | 1112 | 179.3 | 229.2 | 253.3 | 0 | 0 | 112 | 338 |
| lua | 10 | 1400 | 7.0 | 8.9 | 10.4 | 0 | 0 | 89 | 227 |
| lua | 50 | 1635 | 29.9 | 41.5 | 47.4 | 0 | 0 | 95 | 273 |
| lua | 100 | 1608 | 62.2 | 80.1 | 88.5 | 0 | 0 | 89 | 273 |
| lua | 200 | 1505 | 132.2 | 172.3 | 191.3 | 0 | 0 | 98 | 305 |

(The AOT thread counts include the runtime's idle thread-pool threads, which linger longer than in the JIT run; they are not
script threads. Peak working set is a sample every 250 ms and varies between runs because the GC releases memory.)

Observations:

- No errors, no time-out, no transport failure anywhere, up to 200 virtual users with JavaScript and Lua scripts.
- Scripted throughput saturates at about 1100 req/s (JavaScript) and 1500 to 1600 req/s (Lua), whatever N is: a script run is
  CPU-bound (a new QuickJS runtime or Lua state, its prelude and the host bridge), about 5 ms of one core per run for
  JavaScript. Past about 10 virtual users more users only add latency (latency grows linearly with N). A load-test report
  with scripts therefore measures the script cost too; the report should say whether scripts ran.
- Without scripts the sender reaches about 100000 req/s on loopback, so the transport is never the limit.
- Peak working set stays at or under 223 MiB, far below the 1.5 GiB limit.
- Starting a script thread (2 MiB stack, `Thread.Start` until the first instruction runs) costs a median of 49 to 70 µs; it is
  negligible next to the runtime creation.
- The `AOT=` value printed in the machine line is unreliable in the JIT run (the property is set in the project), only the
  published binary counts as the AOT result.

## Decisions

1. **Virtual-user cap: 100** (as planned). 100 and even 200 virtual users give 0 errors, 0 time-outs and at most 223 MiB;
   100 stays the cap (ADR 0019) because above about 10 users scripted throughput does not grow.
2. **Scripts run during load tests: yes** (as planned). No fallback to "scripts skipped" is needed. The report should state the
   script engines' share of the latency (see the observation above) and offer no option to skip.
3. **Shared `HttpClient` pool: no change.** `SocketsHttpHandler` defaults held 200 concurrent loops without a transport
   failure at 100000 req/s without scripts; `MaxConnectionsPerServer` and `PooledConnectionLifetime` stay as they are.
4. **Cost of starting a script thread: about 50 µs** (median 49 to 70 µs over 500 starts, 2 MiB stack), for the docs.

No decision differs from the plan, so M6-03, M6-22 and M6-24 need no change.
