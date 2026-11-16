# M4-47: Core: response differ (headers and body)

Status: todo
Platform: any
Depends on: M4-46

## Goal

A pure service compares two response snapshots: status, headers and body.

## Design decisions

- Read first: ADR 0015 (Diff subject, Body comparison, Header comparison), M4-46, `src/Yaapic.Core/Responses/ResponseFormatter.cs` and `ResponseText.cs`
  (how a body becomes display text, JSON detection, binary detection).
- `public sealed record ResponseSnapshot(int StatusCode, string ReasonPhrase, IReadOnlyList<KeyValueEntry> Headers, string BodyText, string?
  ContentType, DateTimeOffset At)` and `static ResponseSnapshot? TryCreate(HttpResponseData response, DateTimeOffset at)` in
  `Yaapic.Core.Diff`: returns `null` when the body is binary (use the same detection as the response panel) or larger than 1 MiB
  (`ResponseSnapshot.MaxBodyBytes`); the body text is the **formatted** text from `ResponseFormatter` (the Pretty form for JSON, XML, HTML as the panel
  shows it).
- `public sealed record HeaderDiff(string Name, HeaderDiffKind Kind, string? OldValue, string? NewValue)` with `enum HeaderDiffKind { Added,
  Removed, Changed }`, and
  `public sealed record ResponseDiff(bool StatusChanged, string OldStatus, string NewStatus, IReadOnlyList<HeaderDiff> Headers,
  IReadOnlyList<DiffLine> Body, DiffStats BodyStats)`.
- `public static class ResponseDiffer`: `static ResponseDiff Compare(ResponseSnapshot old, ResponseSnapshot current, ResponseDiffOptions options)` with
  `record ResponseDiffOptions(bool IgnoreWhitespace, bool IgnoreVolatileHeaders)` (defaults: false, true).
  - Headers matched by case-insensitive name; a name present several times is compared as the ordered list of values joined with `, `; output
    sorted by name (ordinal ignore case); unchanged headers are not listed.
  - Volatile headers (ADR 0015: `Date`, `Age`, `X-Request-Id`, `X-Amzn-Trace-Id`, `CF-Ray`, `Server-Timing`) are skipped when
    `IgnoreVolatileHeaders`; the list is a public constant `ResponseDiffer.VolatileHeaders`.
  - Body: `LineDiff.Compute(SplitLines(old), SplitLines(current), options.IgnoreWhitespace)`.
  - `StatusChanged` compares the code only (reason phrase text is shown but does not count).

## Out of scope

Context folding, UI, history.

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests`: snapshot null for binary and for > 1 MiB, formatted JSON used; identical responses give no changes; status change;
  each header kind; case-insensitive names; volatile headers skipped or listed; duplicate header values; body line changes with stats; ignore whitespace
  option; options defaults.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): compare two response snapshots`
