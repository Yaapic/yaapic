# M6-22: Core: load-test plan and virtual-user schedule

Status: done
Platform: any
Depends on: M6-03, M6-14

## Goal

The load-test plan and the pure function that says how many virtual users should be active at a given time (ADR 0019).

## Design decisions

- Read first: ADR 0019 ("Plan"), `docs/spikes/m6-load.md` (the virtual-user cap), M6-14 (`RunItem`, `IterationDataRow`).
- Folder `src/Yaapic.Core/LoadTesting/`:
  - `LoadProfile` enum: `Fixed`, `RampUp`.
  - `LoadTestPlan(string Name, IReadOnlyList<RunItem> Items, int VirtualUsers, TimeSpan Duration, LoadProfile Profile, TimeSpan RampUp,
    int ThinkTimeMs, IReadOnlyList<IterationDataRow> Data, ScriptLimits? Limits = null)` with `public const int MaxVirtualUsers = 100`
    (the spike value) and `void Validate()` throwing `ArgumentOutOfRangeException`: users 1..`MaxVirtualUsers`, duration 10..3600 s,
    ramp 1 s..duration (only checked for `RampUp`), think time 0..10000 ms, at least one item.
  - `VirtualUserSchedule` (static): `int TargetUsers(LoadTestPlan plan, TimeSpan elapsed)`: `Fixed` -> `VirtualUsers`; `RampUp` ->
    `elapsed >= RampUp ? VirtualUsers : Math.Max(1, (int)Math.Ceiling(VirtualUsers * elapsed.TotalSeconds / RampUp.TotalSeconds))`;
    0 when `elapsed >= Duration`; negative elapsed counts as 0.
    `TimeSpan StartOffset(LoadTestPlan plan, int userIndex)` (0-based): `Fixed` -> zero; `RampUp` -> the first elapsed time at which
    `TargetUsers` exceeds `userIndex` (user 0 at zero; computed exactly as `RampUp * userIndex / VirtualUsers`, then checked against
    `TargetUsers` so both functions agree).
  - `string DescribeHosts(LoadTestPlan plan, Func<string, string> resolve)`: the distinct hosts (scheme://host:port) of the resolved
    item URLs, comma separated, in first-seen order (`resolve` turns a URL template into a URL; invalid URLs are listed as they are).

## Out of scope

Metrics (M6-23), the runner (M6-24).

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests/LoadTesting/`: `Validate` bounds; `TargetUsers` for Fixed and RampUp (t = 0, mid-ramp with rounding,
  end of ramp, after the duration, negative); `StartOffset` agrees with `TargetUsers` for every user of 7 users over a 10 s ramp; hosts
  listed once in order.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the load-test plan and virtual-user schedule`
