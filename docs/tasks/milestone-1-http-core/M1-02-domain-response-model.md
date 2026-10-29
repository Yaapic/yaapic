# M1-02: Domain response and send-result model

Status: todo
Platform: any
Depends on: M1-01

## Goal

Immutable response and failure types in `Yaapic.Domain.Http`.

## Design decisions

- `sealed record HttpResponseData(int StatusCode, string ReasonPhrase, IReadOnlyList<KeyValueEntry> Headers, byte[] Body,
  TimeSpan Duration, string FinalUrl)`. Headers keep wire order and duplicates; `Enabled` is always true.
  Computed properties: `long SizeBytes => Body.LongLength`; `string? ContentType` (value of the first `Content-Type`
  header, name compared ordinal-ignore-case, else null); `bool IsSuccessStatusCode` (200 to 299 inclusive).
- `enum SendFailureKind { InvalidUrl, Timeout, ConnectionFailed, Canceled, Unknown }`.
- `sealed record SendFailure(SendFailureKind Kind, string Message)`.
- `sealed record SendResult(HttpResponseData? Response, SendFailure? Failure)` with `bool IsSuccess => Response is not null`,
  `static SendResult Success(HttpResponseData response)` and `static SendResult Fail(SendFailureKind kind, string message)`.
  An HTTP 4xx/5xx is a success result (there is a response); only transport problems are failures.
- No validation and no behaviour beyond the members above.

## Out of scope

Timing breakdown (M4), cookies, redirect chain, streaming bodies.

## Acceptance criteria

- [ ] Types exist in `src/Yaapic.Domain/Http/`, 0 warnings.
- [ ] Tests in `Yaapic.Domain.Tests`: `ContentType` lookup is case-insensitive, returns the first match, null when absent;
      `SizeBytes`; `IsSuccessStatusCode` at 199/200/299/300; `Success` and `Fail` set exactly one side and `IsSuccess`
      matches.

## Suggested commit message

`feat(domain): add HTTP response and send result model`
