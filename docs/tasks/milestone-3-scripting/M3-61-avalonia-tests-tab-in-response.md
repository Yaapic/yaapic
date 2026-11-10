# M3-61: Avalonia: Tests tab and script errors in the response panel

Status: done
Platform: unix
Depends on: M3-42, M3-47, M3-54

## Goal

The Avalonia twin of M3-54.

## Design decisions

- Read first: M3-54 (tab rules, pane content, texts, what is not built), `src/Yaapic.App.Avalonia/Views/ResponsePanelView.axaml(.cs)`
  (how the Body and Headers tabs and the failed-state block are built), M3-42.
- A `Tests` tab after `Headers` with the badge (`Tests.BadgeText`, success/error colour), visible only when `Tests.HasTests` or
  `Tests.HasErrors`; the pane: summary + `All | Failed` segmented filter, error blocks above the list, the results list with
  symbol, name, duration, failed message in monospace error colour and origin chip, empty text as in M3-54. Pre-request abort
  reuses the existing failed-state block. The tab never steals the selection.
- Compiled bindings, `BrushMap`, existing styles; no new colours.

## Out of scope

Cookies, timeline, test history.

## Acceptance criteria

- [x] Build `Yaapic.CrossPlatform.slnf` 0 warnings, tests green, AOT publish 0 new IL2xxx/IL3xxx.
- [x] **Runtime check on the published exe**: the M3-54 steps (badge and list with the failing test, filter, post-response error
      block with Body still usable, pre-request abort sends nothing, no tab without tests, dark and light, focus visible).
- [x] `Capture-Avalonia.ps1 -Send -Select Tests` screenshot compared with the mockup; differences in Notes.
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

`feat(avalonia): show test results and script errors in the response panel`

## Notes

- Added: `Tests` tab in `ResponsePanelView.axaml` (badge, summary sentence, All/Failed filter, script-error blocks, test rows with
  symbol, duration, origin chip and failure message, empty text); `ResponseMap.TestOutcome/FilterIndex/NeitherFlag`;
  `OnTestFilterChanged` in the code-behind. The tab is visible through `BoolConverters.Or` of `HasTests`/`HasErrors` and never
  takes the selection.
- Runtime check (AOT exe, win-x64, via `Capture-Avalonia.ps1 -Send`, dark and light), `scripted` fixture: badge `2/3` in the error
  colour, list with the failing test and its `AssertionError` message; a throwing post-response script gives badge `!` and a
  `Script error` block; a throwing pre-request script gives the failed-state block (`Pre-request script failed — pre-request ·
  Scripted: pre boom`, no response); `list-users` shows no Tests tab. The throwing variants were temporary edits of the fixture,
  reverted. Not driven by a pointer (capture tooling cannot click): the `Failed` filter click, the Body tab after a post-response
  error and keyboard focus visuals; the filter wiring is the same VM property covered by `TestResultsViewModelTests`. No
  stub-server request log was checked for the pre-request abort.
- Differences from the mockup: no Cookies and Timeline tabs (out of scope).
- Build 0 warnings; AOT publish 0 warnings; tests 2225 passed, 3 skipped (platform), 0 failed (2228 total).
