# M6-25: Core: load-test report, JSON

Status: done
Platform: any
Depends on: M6-24

## Goal

Write a `LoadTestResult` as a deterministic JSON report (ADR 0019, "Report").

## Design decisions

- Read first: ADR 0019, M6-18 (same writing rules: `Utf8JsonWriter`, indentation, `\n`, no BOM, UTC times, whole milliseconds,
  enum spelling, file name helper and slug), M6-24.
- `src/Yaapic.Core/LoadTesting/LoadTestReportJson.cs`: `public static byte[] Write(LoadTestResult result)` and
  `SuggestedFileName(result)` = `{slug(name)}-load-{yyyyMMdd-HHmmss}.json`.
- Shape (keys in this order): `schema` (`"yaapic-loadtest/1"`), `name`, `startedAt`, `durationMs`, `canceled`, `scriptsSkipped`
  (only when the property exists, M6-24), `plan` { `virtualUsers`, `durationSeconds`, `profile` (`fixed`/`rampUp`), `rampUpSeconds`,
  `thinkTimeMs`, `dataRows`, `requests`: [ { `name`, `path`, `method`, `url` (template) } ] }, `overall` (stats object), `requests`
  (one stats object per request, same order as `plan.requests`, each with `name`), `buckets` [ { `second`, `requests`, `errors`,
  `averageMs`, `activeUsers` } ], `errors` (messages), `note` (`"percentiles computed on the first 2000000 samples"` when truncated,
  else absent). Stats object: `count`, `errors`, `errorRate` (4 decimals), `minMs`, `averageMs` (1 decimal), `maxMs`, `p50Ms`, `p90Ms`,
  `p95Ms`, `p99Ms`, `throughput` (requests per second, 2 decimals). Numbers use invariant culture.

## Out of scope

The view model (M6-26).

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests/LoadTesting/LoadTestReportJsonTests.cs`: exact expected string for a hand-built result (raw string
  literal); truncation note; canceled flag; parses back with `DataNodeJson.Parse`; file name.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): write load-test reports as JSON`
