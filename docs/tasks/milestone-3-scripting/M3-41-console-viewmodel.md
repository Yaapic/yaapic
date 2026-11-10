# M3-41: Console ViewModel

Status: done
Platform: any
Depends on: M3-04

## Goal

The application-wide console: an ordered, filterable, bounded list of what scripts logged, what the network did and which
tests ran, ready for a bottom panel in both UIs.

## Design decisions

- Read first: `design/mockups/MainWindow.dc.html` (the `Console` section around lines 157-175 and the `consoleLines` data:
  columns time, symbol, method, message, origin chip; filter chips "Tout / Log / Warn / Err" and a text filter and "Effacer"),
  `src/Yaapic.ViewModels/History/HistoryViewModel.cs` (a list view model with commands and the `IDialogService` clipboard).
- `src/Yaapic.ViewModels/Console/`:
  - `ConsoleLineViewModel` (immutable record-like class): `TimeText` (`HH:mm:ss.fff` local time), `Symbol` (`›` group header,
    `i` info, `!` warn, `×` error, `↗` network, `✓` passed test, `·` log/debug), `Kind` (`ConsoleLineKind`: `Header`, `Log`, `Info`,
    `Warn`, `Error`, `Network`, `TestPassed`, `TestFailed`), `Message`, `Origin`, `Method` (the HTTP verb for network lines, else
    `""`), `IsError`.
  - `ConsoleViewModel : ObservableObject` (constructor `(IDialogService? dialogs = null, int capacity = 1000)`):
    `ReadOnlyObservableCollection<ConsoleLineViewModel> Lines` (the **filtered** view) and `int TotalCount`, `int WarnCount`,
    `int ErrorCount`; `ConsoleFilter Filter` (`All`, `Log` (log/info/debug/network), `Warn`, `Error`) and `string Search`
    (case-insensitive substring on message and origin); `void AppendRun(ScriptRunResult run)` (adds a `Header` line
    `"<phase> · <origin>"` when the run has output or failed, then its console entries by time, then one `TestPassed`/
    `TestFailed` line per test (`"<name>"`, failed: `"<name> — <message>"`), then a summary line `"Tests: 6 passed, 0 failed"` when
    it ran tests, then an `Error` line for a failed run (`"<status text>: <message> (line N)"`));
    `void AppendNetwork(IEnumerable<ConsoleEntry> entries)`; `void AppendReport(ScriptedSendReport report)` = runs interleaved
    with `NetworkLog` in the order they happened (pre-request runs, network lines, post-response runs; use `At` to order
    entries, stable for equal timestamps); `ClearCommand`; `CopyCommand` (copies the **visible** lines as text, one per line,
    through the dialog clipboard, no-op without a dialog service); `bool IsEmpty`.
  - Capacity: the oldest lines are dropped beyond `capacity`; counts are recomputed from the retained lines.
  - Thread safety: the view model is UI-thread only (the sender result is awaited back onto the UI thread by the caller).
- No persistence: the console is empty at start.

## Out of scope

Views, the panel open/closed state (Shell, M3-47), exporting logs, per-tab consoles.

## Acceptance criteria

- [x] Tests: lines produced for a run with logs, tests (pass/fail), an error and a timeout; header lines; ordering of an
      interleaved report; filter and search combinations and their counts; capacity drop; clear; copy text through
      `FakeDialogService`; `TimeText` format with a fixed `TimeZoneInfo`-independent test (inject the time zone offset through the
      entry's `DateTimeOffset`, assert the local conversion with `ToLocalTime`).
- [x] 0 warnings, XML docs, all tests green.

## Rules

- Every shell command, git included, starts with `rtk` (`rtk bypass <cmd>` only if the condensed output is unusable).
- Wait for long commands (build, test, AOT publish) in the foreground; do not poll.
- Stop every process you launched (published exes, stub servers, `dotnet` build servers you started).
- Check the SonarCloud issues of the files you changed
  (`https://sonarcloud.io/api/issues/search?componentKeys=Yaapic_yaapic&resolved=false`) and fix them before committing.
- Do not skip any acceptance item; if one cannot be met, stop and say why instead of ticking it.
- Commit on `feat/m3-scripting` (no new branch or worktree), GPG-signed
  (`git -c gpg.program="C:\Program Files (x86)\GnuPG\bin\gpg.exe" commit -S`, key of adrien.granger@outlook.com),
  with no trailer or co-author line, then push.

## Suggested commit message

`feat(viewmodels): add the console view model`
