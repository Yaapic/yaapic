# M6-03: ADR 0019: load tests

Status: done
Platform: any
Depends on: M6-01, M6-02

## Goal

Write ADR 0019, the reference for the load-test cards (profiles, virtual users, metrics, report, UI). Docs only. The
decisions below are final, except where the spike M6-02 recorded a different number: use the spike's value then.

## Design decisions (to record in the ADR)

- **Layers.** `Yaapic.Core.LoadTesting`: `VirtualUserSchedule` (pure), `LoadMetrics` (thread-safe aggregator), `LoadTestRunner`
  (the virtual users). It reuses the runner's single-request step (`RunStepExecutor`, M6-16) and `RunVariableState` (M6-15), so
  a load test sends exactly like a run. View model in `Yaapic.ViewModels.LoadTesting`. No new package; charts are drawn by the
  views from normalized points computed by the view model (no chart library).
- **Plan.** Target: one folder, the same request list, selection and order as the Runner setup. Virtual users 1 to **100**
  (spike value). Duration 10 to 3 600 seconds. Profile `Fixed` (every user starts at t = 0) or `RampUp` (target users at elapsed
  time t: `max(1, ceil(N * t / ramp))` until `ramp`, then N; ramp 1 s to the duration). Think time 0 to 10 000 ms between two
  requests of one user (default 0). Optional data file (same reader as the Runner): rows are taken round-robin by a counter
  shared by all users, one row per user iteration. The environment is the active one.
- **A virtual user** loops over the request list (one user iteration = the list once) until the test ends. Scripts run
  (spike decision (2)); `setNextRequest` is ignored in load tests; `skipRequest` skips. Each user has its own `RunVariableState`
  started from the same scopes; variable changes are **never persisted** and nothing reaches the console panel or the history.
- **End.** At the end of the duration no user starts a new request; in-flight requests finish (at most their timeout). Stop
  cancels in-flight requests at once. Ramp-down is out of scope.
- **Sample.** One per request: elapsed time since start (ms), request index, duration (ms), status code (0 when none), `IsError`.
  A sample is an error when the send failed, a pre-request script aborted it, a test failed, or the status is 400 or more
  (load tests count HTTP errors, unlike the Runner). Skipped requests are not samples.
- **Metrics.** Overall and per request: count, errors, error rate, min, average, max, p50, p90, p95, p99 (nearest rank over
  stored durations), throughput (count / elapsed seconds). Per-second buckets: requests, errors, average duration, active users.
  At most 2 000 000 samples are stored; beyond that, counts and min/avg/max keep updating but percentiles use the stored samples
  and the report says "percentiles computed on the first 2000000 samples". The first 20 distinct error messages are kept.
- **Progress.** A snapshot every second (elapsed, active users, totals, last second throughput, buckets) for the view model;
  the charts show requests per second, average response time, errors per second and active users over time.
- **Report.** JSON (`"schema": "yaapic-loadtest/1"`, deterministic like the run report: plan, overall metrics, per-request metrics,
  buckets, error messages, URL templates only). Saved through `IDialogService.SaveFileAsync`.
- **UI.** A Load test window (non-modal, one at a time) opened from the folder context menu ("Load test...") and the palette:
  configuration (users, duration, profile, ramp, think time, data file, request list), Start/Stop, live tiles (requests, throughput,
  average, p95, error rate, active users), four small line charts, a per-request table, the error messages, Export.
- **Limits and safety.** Load tests can hurt servers: the window shows the target host(s) and the confirmation "Start a load test
  against {hosts}?" before the first start of each window session. No distributed load, no cloud agents.
- **Out of scope**: spike and peak profiles, thresholds/SLAs, comparing runs, saving load-test configurations, HTML reports.

## Out of scope

Code, UI.

## Acceptance criteria

- [x] `docs/adr/0019-load-tests.md` contains every decision above with the spike numbers (status Accepted; options considered:
  reusing the runner step versus a lighter HTTP-only loop, a chart library versus drawing polylines, storing every sample versus
  histograms); `docs/adr/README.md` lists it; it links `docs/spikes/m6-load.md`.
- [x] No code changed; the build still has 0 warnings.

## Suggested commit message

`docs(adr): add ADR 0019 load tests`
