# M1-18: Response panel ViewModel

Status: todo
Platform: any
Depends on: M1-09, M1-16

## Goal

Display-ready state of a response: status, time, size, Pretty/Raw body, headers, warnings and errors.

## Design decisions

- `partial class ResponsePanelViewModel : ObservableObject` in `Yaapic.ViewModels.Responses`.
- `enum ResponseState { Empty, Sending, Completed, Failed }`, `enum StatusCategory { Informational, Success, Redirect,
  ClientError, ServerError }` (by hundreds; anything else -> `Informational`).
- Methods: `void ShowSending()`, `void ShowReport(SendReport report)`, `void Clear()` (back to `Empty`).
- Observable read-only properties: `State`, `StatusText` (`"200 OK"`, reason omitted when empty: `"204"`),
  `StatusCategory`, `DurationText`, `SizeText`, `ViewMode` (`BodyViewMode`, settable, default `Pretty`, kept across
  responses), `BodyText`, `BodyLanguage`, `IsBinary`, `BinaryText` (`"Binary content, 1.5 KB"`),
  `IReadOnlyList<KeyValueEntry> Headers`, `ErrorMessage` (failure message, null otherwise), `UnresolvedWarning`
  (`"Unresolved variables: a, b"` or null). Changing `ViewMode` reformats the current response with `ResponseFormatter`.
- `DurationText`: below 1000 ms `"{ms} ms"` (integer, rounded); otherwise `"{s:0.00} s"`. `SizeText`: below 1024 `"{n} B"`,
  below 1024² `"{n/1024:0.0} KB"`, else `"{n/1024²:0.0} MB"`. Invariant culture for both.
- A failed report: `State = Failed`, `ErrorMessage` set, body and headers cleared, `UnresolvedWarning` still set.

## Out of scope

Copy/save body, search, timeline, cookies tab, code editor (plain text in M1).

## Acceptance criteria

- [ ] Tests: status text and category for 101/204/302/404/503; duration 5 ms / 999.6 ms / 1234 ms; size 0 B / 1023 B /
      1536 B / 3 MB; Pretty vs Raw switch reformats JSON; binary body; failure state; unresolved warning text; `Clear`.
- [ ] 0 warnings.

## Suggested commit message

`feat(viewmodels): add response panel view model`
