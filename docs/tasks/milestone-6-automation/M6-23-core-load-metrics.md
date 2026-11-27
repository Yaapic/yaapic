# M6-23: Core: load metrics aggregator

Status: done
Platform: any
Depends on: M6-03, M6-22

## Goal

Collect load-test samples from many threads and compute the metrics, buckets and snapshots of ADR 0019 ("Sample", "Metrics", "Progress").

## Design decisions

- Read first: ADR 0019, M6-22.
- Files in `src/Yaapic.Core/LoadTesting/`:
  - `LoadSample(long ElapsedMs, int RequestIndex, int DurationMs, int StatusCode, bool IsError)` (readonly record struct).
  - `LoadStats(long Count, long Errors, double ErrorRate, int MinMs, double AverageMs, int MaxMs, int P50Ms, int P90Ms, int P95Ms,
    int P99Ms, double Throughput)` with `static LoadStats Empty`.
  - `LoadBucket(int Second, int Requests, int Errors, double AverageMs, int ActiveUsers)`.
  - `LoadSnapshot(TimeSpan Elapsed, int ActiveUsers, LoadStats Overall, double LastSecondThroughput, IReadOnlyList<LoadBucket> Buckets)`.
  - `LoadMetrics` (public sealed class, thread-safe with one `lock`): constructor `(int requestCount, int maxStoredSamples = 2_000_000)`;
    `void Add(LoadSample sample)`; `void AddError(string message)` (keeps the first 20 distinct messages, ordinal);
    `void SetActiveUsers(long elapsedMs, int users)` (recorded on the bucket of that second, the last value of a second wins);
    `LoadSnapshot Snapshot(TimeSpan elapsed)`; `LoadStats Overall(TimeSpan elapsed)`, `LoadStats ForRequest(int index, TimeSpan elapsed)`;
    `IReadOnlyList<string> ErrorMessages`; `bool SamplesTruncated`.
- Rules: counts, errors, min, sum and max are always updated; durations are stored (overall and per request) only while fewer than
  `maxStoredSamples` are stored in total (`SamplesTruncated` becomes true after). Percentiles: nearest rank on the stored durations,
  `rank = ceil(p/100 * n)` (1-based), 0 when none. `ErrorRate = Errors / Count` (0 when no sample). `Throughput = Count / elapsed seconds`
  (0 when elapsed is 0). Buckets: second `ElapsedMs / 1000`, every second from 0 to the last one present (empty seconds have zeros and
  the previous second's active users). `LastSecondThroughput` = requests of the last complete second (the bucket before the current one).
- Sorting for percentiles happens in `Overall`/`ForRequest`/`Snapshot` on a copy (do not sort inside `Add`).

## Out of scope

Producing samples (M6-24), JSON (M6-25).

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests/LoadTesting/LoadMetricsTests.cs`: exact stats for a known set (percentiles by nearest rank,
  including n = 1 and n = 100); error rate; per-request stats; buckets with a gap; active users per bucket; truncation at a small cap
  keeps counts exact; 20 distinct error messages kept; 8 threads adding 10 000 samples each give exact counts.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): aggregate load-test metrics`
