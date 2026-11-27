# M6-18: Core: run report, JSON

Status: done
Platform: any
Depends on: M6-14

## Goal

Write a `RunResult` as a deterministic JSON report (ADR 0018, "Reports").

## Design decisions

- Read first: ADR 0018, M6-14, an M5 exporter that writes JSON with `Utf8JsonWriter` (`src/Yaapic.Core/Export/Postman/`), the
  determinism rules of ADR 0016 (UTF-8 without BOM, `\n`, two-space indentation, stable key order).
- `src/Yaapic.Core/Runner/RunReportJson.cs`: `public static byte[] Write(RunResult result)`. No `JsonSerializer`, only
  `Utf8JsonWriter` with `Indented = true` (then normalize line ends to `\n` if the writer emits `\r\n` on Windows, as the M5 exporters do).
- Shape (keys in this order):
  ```json
  {
    "schema": "yaapic-run/1",
    "name": "...", "startedAt": "2026-11-24T10:00:00.000Z", "durationMs": 1234, "status": "completed", "error": null,
    "options": { "iterations": 2, "delayMs": 0, "stopOnFailure": false, "persistVariables": true, "dataRows": 3 },
    "summary": { "sent": 5, "skipped": 1, "errors": 0, "failedRequests": 1, "testsPassed": 9, "testsFailed": 1, "averageResponseMs": 42 },
    "iterations": [ { "index": 0, "requests": [ {
        "id": "...", "name": "...", "path": "Collection/Sub", "method": "GET", "url": "{{baseUrl}}/users",
        "outcome": "passed", "status": 200, "durationMs": 40, "sizeBytes": 512, "error": null,
        "tests": [ { "name": "...", "passed": true, "message": null, "durationMs": 1 } ],
        "console": [ { "at": "...", "level": "info", "source": "script", "message": "...", "origin": "..." } ] } ] } ]
  }
  ```
  Enum values are lowercase camel case of the member name (`stoppedOnFailure`). Times are UTC ISO 8601 with milliseconds and `Z`;
  durations are whole milliseconds (rounded half away from zero). `status` is `null` when there is none.
- File name helper: `public static string SuggestedFileName(RunResult result)` = `{slug(name)}-run-{yyyyMMdd-HHmmss}.json` (UTC start;
  reuse the slug helper of the workspace file names if Core has one, else lower-case letters, digits and `-`).

## Out of scope

JUnit (M6-19), saving (view model).

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests/Runner/RunReportJsonTests.cs`: a hand-built result serializes to an exact expected string (raw string
  literal, `\n` line ends); null fields; every enum spelling; the output parses back with `DataNodeJson.Parse`; no BOM; the file name.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): write run reports as JSON`
