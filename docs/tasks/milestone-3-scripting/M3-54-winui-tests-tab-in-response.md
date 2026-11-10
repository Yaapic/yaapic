# M3-54: WinUI: Tests tab and script errors in the response panel

Status: done
Platform: windows
Depends on: M3-42, M3-47

## Goal

The response panel gets a `Tests` tab with a pass/fail count badge (`6/6`), the list of results, and a visible script-error
state, as in the mockup (`Body, Headers 14, Cookies 1, Tests 6/6, Timeline`).

## Design decisions

- Read first: `design/mockups/MainWindow.dc.html` (`resTabs`: `['Tests','6/6']`), `ResponseStates.dc.html` (error blocks),
  `DSComposants2.dc.html` (chips and badges, list rows, InfoBar), `src/Yaapic.App.WinUI/Views/ResponsePanelView.xaml(.cs)` (the
  `HeadersTab` toggle, how Body/Headers panes switch, the failed-state block and `ErrorMessage`), M3-42 (`TestResultsViewModel`,
  `Tests.BadgeText`, `Errors`, `ShowScripted`).
- Tab: `TestsTab` toggle (`YaapicPanelToggleTabStyle`) after `Headers`, text `Tests`, badge `Tests.BadgeText` (success colour when
  `AllPassed`, error colour when any failed or `!`), visible only when `Tests.HasTests` or `Tests.HasErrors`; the `Cookies` and
  `Timeline` tabs of the mockup are **not** built (no cookie jar, timeline is milestone 4).
- Pane: a summary row (`Tests.Summary` text `6 of 6 passed` / `4 of 6 passed`, a small all/failed segmented filter bound to `Filter`),
  then a `ListView` of `Items`: row = symbol (`✓` success colour, `×` error colour), test name, duration (secondary), failed rows
  show the message beneath in monospace error colour; origin chip at the right. A second list/`ItemsControl` above it for
  `Errors` (script errors) as error blocks (`StatusText`, origin, message, location), using the failed-state styling of the
  existing error block. Empty text `"No tests ran. Add pm.test(...) to the post-response script."`.
- A **pre-request abort** reuses the existing failed-state block (`ErrorMessage` from `ShowScripted`), no tabs.
- When tests exist and `Body` is the selected tab nothing changes; after a send with a failing test the tab does **not** steal
  the selection (the badge is the signal).
- Compiled bindings only; reuse brushes and styles; new `BrushMap` helpers only if needed.

## Out of scope

Cookies tab, timeline, test history, re-running tests.

## Acceptance criteria

- [x] Build 0 warnings, tests green, AOT publish 0 IL2xxx/IL3xxx.
- [x] **Runtime check (AOT exe)**, `scripted` fixture: after Send the Tests tab shows the badge, the list with the failing test and
      its message, filter `Failed` works; a post-response script that throws shows an error block and keeps the Body tab
      working; a pre-request script that throws shows the failed-state block and sends nothing (the stub server's request log shows
      no hit); a request without tests shows no Tests tab; dark and light; keyboard focus visible on the tab and filter. Record
      in Notes.
- [x] `Capture-WinUI.ps1 -Select Tests` screenshot compared with the mockup; differences in Notes.
- [x] No process left running.

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

`feat(winui): show test results and script errors in the response panel`

## Notes

- Added: `TestsTab` toggle and `TestsPane` in `ResponsePanelView` (summary sentence, All/Failed filter, script-error blocks, test rows
  with symbol, duration, origin chip and failure message, empty text); `TestResultsViewModel.SummaryText` (`4 of 6 passed`, the
  existing `Summary` stays `4/6` for the badge); `ListMap.TestItems/ScriptErrors`, `VisibilityMap.WhenEither/WhenNeitherFlag`,
  `BrushMap.TestOutcome`. `Capture-WinUI.ps1 -Select ... -SelectAfterSend` now also toggles `ToggleButton`s.
- Runtime check (AOT exe, dark and light), `scripted` fixture: badge `2/3` in the error colour, list with the failing test and its
  `AssertionError` message, `Failed` filter leaves only that row; the Tests tab does not take the selection after a send; a throwing
  post-response script gives badge `!` and a `Script error` block, Body tab still works; a throwing pre-request script gives the
  failed-state block (`Pre-request script failed — pre-request · Scripted: pre boom`) and a logging stub on 5199 recorded no hit;
  `list-users` (no tests) shows no Tests tab; keyboard focus ring visible on the `Failed` segment and on the response tabs.
- Differences from the mockup: no Cookies and Timeline tabs (out of scope); test rows are a little taller than the mockup because of
  the default list item metrics; the empty text only shows while the Tests tab stays selected across a send without tests.
- Build 0 warnings; AOT publish 0 IL2xxx/IL3xxx; tests 2201 passed, 3 skipped (platform), 0 failed.
