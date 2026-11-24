# M6-02: Spike: concurrent scripted sends under load

Status: done
Platform: any
Depends on: none

## Goal

Load tests (ADR 0019) run up to 100 virtual users, each sending through `ScriptedRequestSender` with the real script
engines. Every script run creates a fresh QuickJS runtime or Lua state on a **dedicated thread** (2 MiB stack). Measure that
this holds at load, and record the numbers the load-test cards rely on.

## Design decisions

- Read first: `src/Yaapic.Scripting/QuickJs/QuickJsEngine.cs`, `src/Yaapic.Scripting/Lua/LuaEngine.cs` (thread per run),
  `src/Yaapic.Infrastructure/Http/HttpClientTransport.cs` and `DefaultHttpClient.cs` (one shared `HttpClient`, pool settings),
  `src/Yaapic.Core/Scripting/ScriptedRequestSender.cs`, `tests/Yaapic.Infrastructure.Tests/EndToEnd/LoopbackServer.cs` and
  `ScriptedShellFixture.cs`.
- Throwaway console program under `docs/spikes/m6-load/` (not in `Yaapic.sln`; copy the AOT settings of `docs/spikes/m5-yaml-tree`),
  referencing `Yaapic.Core`, `Yaapic.Infrastructure` and `Yaapic.Scripting` by project path. It starts a loopback server on
  `127.0.0.1` (a small `TcpListener` answering `200 {"ok":true}` with keep-alive, written in the spike), then runs N concurrent
  loops for 20 seconds, each loop calling `ScriptedRequestSender.SendAsync` on a GET with (a) no script, (b) a JavaScript
  pre-request and post-response script (`yaapic.variables.set('t', Date.now()); yaapic.test('ok', () => yaapic.expect(yaapic.response.code).to.equal(200));`),
  (c) the same in Lua. N = 10, 50, 100, 200.
- Measure and print, for each (script kind, N): requests per second, p50/p95/p99 latency, errors, peak working set, peak thread
  count (`Process.GetCurrentProcess().Threads.Count` sampled every 250 ms), and whether any run reported `TimedOut` (the 5 s
  script limit) or a transport failure. Run it as a JIT build and as a Native AOT publish (`PublishAot=true`, 0 IL2xxx/IL3xxx).
- Decide and record (later cards depend on these): (1) the virtual-user cap (the plan: **100**; lower it if 100 VUs with
  JavaScript scripts give errors, time-outs or a working set above 1.5 GiB); (2) whether scripts run during load tests (the plan:
  **yes**; if not viable, the fallback is "scripts are skipped in load tests and the report says so"); (3) whether the shared
  `HttpClient` needs a pool change (`MaxConnectionsPerServer`, `PooledConnectionLifetime`) for load (the plan: no change);
  (4) the cost of starting a script thread (median microseconds) for the docs.
- Output: `docs/spikes/m6-load.md` with the results table, the machine (CPU, cores, RAM, OS), the AOT result and decisions (1) to (4).
  **If a decision differs from the plan**, write it in the document and add a note at the end of this card: the parent session
  then updates M6-03, M6-22 and M6-24 before they start.

## Out of scope

Yaapic production code, the load-test runner (M6-24).

## Acceptance criteria

- [x] The spike program and `docs/spikes/m6-load.md` committed; the document lists the measurements and the four decisions.
- [x] `Yaapic.sln` unchanged, build still 0 warnings; no process left running (the spike and its server are stopped).

## Suggested commit message

`docs(spike): measure concurrent scripted sends under load`
