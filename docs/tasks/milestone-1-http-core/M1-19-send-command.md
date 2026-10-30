# M1-19: Send and cancel commands

Status: done
Platform: any
Depends on: M1-17, M1-18

## Goal

Wire `RequestSender` into the request editor: Send, Cancel, and a response panel per tab.

## Design decisions

- `interface IActiveEnvironmentProvider { EnvironmentDefinition? Current { get; } }` in `Yaapic.ViewModels.Requests`
  (implemented by M1-22; tests use a stub).
- `RequestEditorViewModel` gains constructor dependencies `RequestSender sender` and
  `IActiveEnvironmentProvider environments` (update `FromDefinition`/`CreateNew` signatures and the M1-17 tests to pass
  them; use `FakeHttpTransport` from `Yaapic.TestDoubles`).
- `ResponsePanelViewModel Response { get; }`, one per editor (each tab keeps its last response).
- `IAsyncRelayCommand SendCommand` (`[RelayCommand(IncludeCancelCommand = true)]`, which generates `SendCancelCommand`;
  expose it also as `CancelCommand` property). Send: `Response.ShowSending()`, call
  `sender.SendAsync(ToDefinition(), environments.Current, token)`, then `Response.ShowReport(report)`. Unsaved edits are
  sent as they are (no save required).
- `bool IsSending` mirrors `SendCommand.IsRunning`. `SendCommand` cannot execute while sending or when `Url` is blank.
- Cancellation produces the `Canceled` failure from the transport and the panel shows "Request canceled".

## Out of scope

History, keyboard shortcuts (views), scripts, send-and-download.

## Acceptance criteria

- [x] Tests: send passes the current (unsaved) definition and the active environment to the transport; panel goes
      `Sending` -> `Completed`; transport failure -> `Failed`; cancel while a fake transport waits on the token -> canceled
      message; `CanExecute` false with blank URL and while sending.
- [x] 0 warnings.

## Suggested commit message

`feat(viewmodels): add send and cancel commands`
