# M3-54: WinUI: Tests tab and script errors in the response panel

Status: todo
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

- [ ] Build 0 warnings, tests green, AOT publish 0 IL2xxx/IL3xxx.
- [ ] **Runtime check (AOT exe)**, `scripted` fixture: after Send the Tests tab shows the badge, the list with the failing test and
      its message, filter `Failed` works; a post-response script that throws shows an error block and keeps the Body tab
      working; a pre-request script that throws shows the failed-state block and sends nothing (the stub server's request log shows
      no hit); a request without tests shows no Tests tab; dark and light; keyboard focus visible on the tab and filter. Record
      in Notes.
- [ ] `Capture-WinUI.ps1 -Select Tests` screenshot compared with the mockup; differences in Notes.
- [ ] No process left running.

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
