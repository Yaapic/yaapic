# M2-53: Domain: history entry

Status: done
Platform: any
Depends on: M2-01

## Goal

The domain record of one sent request.

## Design decisions

- In `Yaapic.Domain.History` (new folder): `public sealed record HistoryEntry(Guid Id, DateTimeOffset At,
  HttpRequestDefinition Request, int? StatusCode, string StatusText, TimeSpan Duration, long SizeBytes,
  SendFailureKind? FailureKind, string? FailureMessage)`.
  - `bool IsSuccess` = `StatusCode is not null`.
  - `static HistoryEntry From(HttpRequestDefinition request, SendResult result, DateTimeOffset at)`: new id; for a
    successful result, the response's status code, reason phrase, elapsed time and `SizeBytes`, failure members null; for a
    failure, `StatusCode` null, `StatusText` `""`, `Duration` zero, `SizeBytes` 0, and the failure kind and message.
    `request` is stored as given (the unresolved definition, exactly as the user sent it, ADR 0008).
  - `bool IsEquivalentTo(HistoryEntry other)` comparing every member, the request through its own `IsEquivalentTo`.
- `At` is stored as UTC (`ToUniversalTime()` in `From`).

## Out of scope

Storage, ViewModels, canceled sends (the recorder skips them, M2-56).

## Acceptance criteria

- [x] Tests in `Yaapic.Domain.Tests`: `From` for a success and for each `SendFailureKind`; UTC conversion;
      `IsSuccess`; `IsEquivalentTo` true/false cases.
- [x] 0 warnings.

## Suggested commit message

`feat(domain): add history entry`
