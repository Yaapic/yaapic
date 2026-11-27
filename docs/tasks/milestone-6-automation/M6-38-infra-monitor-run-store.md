# M6-38: Infrastructure: monitor run history store

Status: done
Platform: any
Depends on: M6-36

## Goal

Keep monitor runs per user in `.yaapic/monitor-runs.jsonl` (ADR 0021, "History").

## Design decisions

- Read first: ADR 0021, M6-36 (`IMonitorRunStore`), `src/Yaapic.Infrastructure/Storage/FileHistoryStore.cs` and `HistoryLineDto.cs`
  (JSON Lines, source-generated `JsonSerializerContext`, trimming, corrupt lines), `docs/file-format.md` ("History file").
- `src/Yaapic.Infrastructure/Storage/MonitorRunLineDto.cs` (+ `MonitorFailureDto`) and a source-generated context (add to the existing
  storage JSON context if there is one, else `MonitorRunJsonContext`); camelCase names: `monitorId`, `at` (ISO 8601 UTC), `durationMs`,
  `status` (`passed`/`failed`/`error`), `requests`, `failedRequests`, `testsPassed`, `testsFailed`, `message`, `failures` [`request`,
  `outcome`, `status`, `detail`].
- `FileMonitorRunStore` (`.yaapic/monitor-runs.jsonl` under the workspace): `AppendAsync` appends one line, then, when that monitor now has
  more than 200 lines, rewrites the file without its oldest lines (write to a temporary file then replace, as `FileHistoryStore` trims);
  `ListAsync` reads newest first (by `at`, then file order descending); corrupt lines are skipped; `DeleteAsync` rewrites without that
  monitor's lines. All calls are serialised with a `SemaphoreSlim`.
- `docs/file-format.md`: a "Monitor run history" section next to "History file".

## Out of scope

The scheduler (M6-39).

## Acceptance criteria

- [x] Tests in `Yaapic.Infrastructure.Tests`: append and list newest first; 201 runs keep 200 for that monitor and do not touch another
  monitor's runs; `LatestAsync`; delete; corrupt line skipped; file is inside `.yaapic/` (git-ignored); parallel appends do not lose lines.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green; no IL2xxx/IL3xxx.

## Suggested commit message

`feat(infrastructure): keep the monitor run history`
