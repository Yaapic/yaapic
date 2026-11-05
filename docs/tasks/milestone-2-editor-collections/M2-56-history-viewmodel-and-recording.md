# M2-56: History ViewModel and recording

Status: done
Platform: any
Depends on: M2-54, M2-11

## Goal

Every completed send is recorded, and the history is available as a filtered, day-grouped list.

## Design decisions

- `RequestEditorViewModel` raises `event EventHandler<RequestSentEventArgs>? Sent` after `Response.ShowReport(report)`;
  `sealed class RequestSentEventArgs(HttpRequestDefinition Request, SendReport Report)` in `Yaapic.ViewModels.Requests`
  (`Request` = `ToDefinition()` as sent). Not raised when the send was canceled by the user.
- `partial class HistoryViewModel : ObservableObject` in `Yaapic.ViewModels.History`, constructor
  `(IHistoryStore store, TimeProvider time)`:
  - `Task RecordAsync(HttpRequestDefinition request, SendReport report)`: skips `SendFailureKind.Canceled`; builds
    `HistoryEntry.From(request, report.Result, time.GetUtcNow())`, appends to the store and inserts it at the top of the
    list; a `StorageException` is put in `LastError` and never thrown (the method is called without awaiting from the
    composition root).
  - `Task RefreshAsync()`: loads the store.
  - `ObservableCollection<HistoryGroup> Groups` (the filtered view): `sealed class HistoryGroup(string Title,
    ObservableCollection<HistoryItemViewModel> Items)`; groups by the **local date** of `At` (using
    `time.LocalTimeZone`), newest day first, newest item first inside; titles "Today", "Yesterday", else `yyyy-MM-dd`.
    `HistoryItemViewModel`: `Entry`, `Id`, `MethodText`, `Url` (the request URL as stored), `Name`, `StatusText`
    ("200 OK" or the failure message), `bool IsFailed`, `StatusCategory` (the same mapping as `ResponsePanelViewModel`:
    status code / 100 -> `Success`, `Redirect`, `ClientError`, `ServerError`, else `Informational`; meaningless when
    `IsFailed`, views use `IsFailed` first), `TimeText` (`HH:mm` local), `DurationText`.
  - Observable `FilterText` (trimmed, ordinal-ignore-case `Contains` on method text, name, URL or status text), `bool
    IsEmpty` (no entry at all), `string? LastError`.
- Commands that act on entries are in M2-57.

## Out of scope

Open/delete/clear commands, shell and composition wiring (M2-57), views.

## Acceptance criteria

- [x] Tests in `Yaapic.ViewModels.Tests` with `InMemoryHistoryStore` and a fake `TimeProvider`: record a success and a
      failure; canceled sends are not recorded; grouping across today, yesterday and an older day with a non-UTC local
      zone; ordering; filter by method, name, URL and status; `IsEmpty`; a failing store sets `LastError` without
      throwing; the editor raises `Sent` after a send with the request as sent and not on cancel (use `FakeHttpTransport`).
- [x] 0 warnings.

## Suggested commit message

`feat(viewmodels): record sent requests and expose the history list`
