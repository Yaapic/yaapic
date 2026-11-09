# M3-42: Test results ViewModel and response panel integration

Status: done
Platform: any
Depends on: M3-04, M3-12

## Goal

The response panel can show the tests a send produced (list, summary, filters) and the script errors, and the pre-request
abort state.

## Design decisions

- Read first: `src/Yaapic.ViewModels/Responses/ResponsePanelViewModel.cs` (`ShowSending`, `ShowReport`, `Clear`, `State`,
  `ErrorMessage`, `UnresolvedWarning`), `ResponseState.cs`, M3-12 (`ScriptedSendReport`), mockup `MainWindow.dc.html` (response
  tabs `Body / Headers / Cookies / Tests 6/6 / Timeline`; the "Tests" tab shows a pass/fail list).
- `src/Yaapic.ViewModels/Responses/`:
  - `TestResultItemViewModel`: `Name`, `Passed`, `Message`, `DurationText` (`"3 ms"`), `Origin`, `StatusText` (`"Passed"`/`"Failed"`),
    `Symbol` (`✓`/`×`).
  - `ScriptErrorItemViewModel`: `Origin`, `StatusText` (`"Script error"`, `"Timed out"`, `"Memory limit"`, `"Canceled"`, `"Engine unavailable"`),
    `Message`, `Location` (`"line 3"` or empty).
  - `TestResultsViewModel : ObservableObject`: `Items` (filtered), `Errors`, `int PassedCount`, `FailedCount`, `TotalCount`,
    `bool HasTests`, `bool HasErrors`, `bool AllPassed`, `string Summary` (`TestSummary.Format`, e.g. `"6/6"`), `string BadgeText`
    (summary, or `"!"` when there are script errors and no tests), `TestFilter Filter` (`All`, `Failed`), `void Show(IReadOnlyList<ScriptRunResult> runs)`,
    `void Clear()`.
- `ResponsePanelViewModel` gains `TestResultsViewModel Tests { get; }` and
  `void ShowScripted(ScriptedSendReport report)`: when `report.AbortedBy` is set, state becomes the existing error state with
  `ErrorMessage = "Pre-request script failed — <origin>: <message>"` (the cases of `Unavailable`/`TimedOut`... use the same
  status texts) and no body; otherwise it calls the existing `ShowReport(report.Report!)` and then `Tests.Show(report.Runs)`.
  `ShowSending` and `Clear` clear `Tests`. The existing `ShowReport(SendReport)` stays and clears `Tests` (callers without
  scripts).
- A failed **post-response** run does not change the state; its error appears in `Tests.Errors`.

## Out of scope

Views, the console, applying variable changes.

## Acceptance criteria

- [x] Tests: items and counts for passed/failed mixes, summary text, filter, errors list per status, `ShowScripted` for success
      with tests, for a post-response error (response kept, error listed), for a pre-request abort (error state, no body),
      `ShowSending`/`Clear` reset, no-script report leaves `Tests` empty; existing response panel tests unchanged and green.
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

`feat(viewmodels): show test results and script errors in the response panel`
