# ADR 0019: Load tests

Status: Accepted

See also: [ADR 0018](0018-collection-runner.md), [ADR 0009](0009-scripting.md),
[spike M6-02](../spikes/m6-load.md). The load-test cards of milestone 6 implement this ADR; it is their reference.

## Context

Milestone 6 adds load tests: several virtual users send the requests of a folder repeatedly for a fixed duration, and the
user reads live metrics and exports a report. A load test must send exactly like the editor and the Runner, stay testable
without UI or network, and not harm servers by accident.

## Options considered

1. **How a virtual user sends.**
   - *Reuse the runner's single-request step, `RunStepExecutor`, and `RunVariableState` (chosen).* A load test then sends
     exactly like a run (authentication, certificates, redaction, scripts). The spike measured 0 errors up to 200 users.
   - A lighter HTTP-only loop. Rejected: duplicates the pipeline and would skip scripts, which load tests must run.
2. **Charts.**
   - *Polylines drawn by the views from normalized points computed by the view model (chosen).* No dependency, AOT-safe,
     four small line charts only.
   - A chart library. Rejected: a new package, and trimming and AOT risk, for four simple charts.
3. **Sample storage.**
   - *Store every duration up to 2 000 000 samples (chosen).* Exact nearest-rank percentiles; about 16 MB of memory at
     the cap.
   - Histograms (HDR-like). Rejected: approximate percentiles and more code, with no need at the supported scale.

## Native AOT risks

None new: no reflection, no package, JSON through `System.Text.Json` source generators. The spike published a Native AOT
build with 0 warnings (no IL2xxx/IL3xxx) and ran concurrent scripted sends cleanly.

## Decision

### Layers

`Yaapic.Core.LoadTesting`: `VirtualUserSchedule` (pure), `LoadMetrics` (thread-safe aggregator) and `LoadTestRunner` (the
virtual users). It reuses `RunStepExecutor` (M6-16) and `RunVariableState` (M6-15). The view model lives in
`Yaapic.ViewModels.LoadTesting`. No new package; the views draw the charts.

### Plan

- **Target:** one folder, with the same request list, selection and order as the Runner setup.
- **Virtual users:** 1 to **100** (spike decision 1: 100 and 200 users gave 0 errors, 0 time-outs and at most 223 MiB, but
  above about 10 users scripted throughput stops growing, so 100 stays the cap).
- **Duration:** 10 to 3 600 seconds.
- **Profile:** `Fixed` (every user starts at t = 0) or `RampUp`: the target number of users at elapsed time t is
  `max(1, ceil(N * t / ramp))` until `ramp`, then N; the ramp is 1 s to the duration.
- **Think time:** 0 to 10 000 ms between two requests of one user (default 0).
- **Data file (optional):** same reader as the Runner. Rows are taken round-robin by a counter shared by all users, one row
  per user iteration.
- **Environment:** the active one.

### A virtual user

It loops over the request list (one user iteration is the list once) until the test ends. Scripts run (spike decision 2: no
fallback to "scripts skipped"). `setNextRequest` is ignored; `skipRequest` skips. Each user has its own `RunVariableState`
started from the same scopes. Variable changes are **never persisted**, and nothing reaches the console panel or the history.

Scripts dominate the cost: the spike measured about 1 100 req/s (JavaScript) and 1 500 to 1 600 req/s (Lua) whatever N is,
because a script run is CPU-bound (about 5 ms of one core per JavaScript run), against about 100 000 req/s without scripts on
loopback. The report therefore states whether scripts ran, and offers no option to skip them. Starting a script thread costs
about 50 µs (median 49 to 70 µs, 2 MiB stack), negligible. The shared `HttpClient` pool is unchanged (spike decision 3).

### End

At the end of the duration no user starts a new request; in-flight requests finish (at most their timeout). Stop cancels
in-flight requests at once. Ramp-down is out of scope.

### Sample

One per request: elapsed time since start (ms), request index, duration (ms), status code (0 when none), `IsError`. A sample is
an error when the send failed, a pre-request script aborted it, a test failed, or the status is 400 or more (load tests count
HTTP errors, unlike the Runner). Skipped requests are not samples.

### Metrics

Overall and per request: count, errors, error rate, min, average, max, p50, p90, p95, p99 (nearest rank over stored
durations), throughput (count / elapsed seconds). Per-second buckets: requests, errors, average duration, active users. At most
2 000 000 samples are stored; beyond that, counts and min/avg/max keep updating but percentiles use the stored samples, and
the report says "percentiles computed on the first 2000000 samples". The first 20 distinct error messages are kept.

### Progress

A snapshot every second (elapsed, active users, totals, last-second throughput, buckets) for the view model. The charts show
requests per second, average response time, errors per second and active users over time.

### Report

JSON, `"schema": "yaapic-loadtest/1"`, deterministic like the run report: plan, overall metrics, per-request metrics, buckets,
error messages, URL templates only (no resolved secrets). Saved through `IDialogService.SaveFileAsync`.

### UI

A Load test window (non-modal, one at a time) opened from the folder context menu ("Load test...") and the palette:
configuration (users, duration, profile, ramp, think time, data file, request list), Start/Stop, live tiles (requests,
throughput, average, p95, error rate, active users), four small line charts, a per-request table, the error messages, Export.

### Limits and safety

Load tests can hurt servers: the window shows the target host(s) and asks "Start a load test against {hosts}?" before the first
start of each window session. No distributed load, no cloud agents.

### Out of scope

Spike and peak profiles, thresholds/SLAs, comparing runs, saving load-test configurations, HTML reports.
