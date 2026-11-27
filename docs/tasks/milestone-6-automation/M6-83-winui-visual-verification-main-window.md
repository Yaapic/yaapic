# M6-83: WinUI visual verification of the automation views in the main window

Status: todo
Platform: windows
Depends on: M6-81

## Goal

Recapture the milestone 6 WinUI screenshots now that the tools are views of the main window (ADR 0023), check the new rail entries and
the layout at the default and minimum sizes, and update the screenshot README, as M6-61 did for the tool windows. The screenshots of
M6-61 are replaced; M6-61 itself is not edited (its remaining human review applies to the new captures).

## Design decisions

- Read first: M6-61 (method, notes, defects D1 to D6), `tools/screenshots/Capture-M6.ps1`, `tools/screenshots/Capture-WinUI.ps1` (steps:
  `invoke:` takes a name **or an AutomationId**, `rclick:`, `front:`, `-Window`), `docs/screenshots/m6/README.md`, M6-77 (rail AutomationIds
  `RailRunner`, `RailLoadTest`, `RailMockServers`, `RailMonitors`).
- `Capture-M6.ps1`: same eight screens and file names (`runner-setup`, `runner-results`, `load-test-running`, `load-test-done`,
  `mock-servers`, `monitors`, `folder-menu`, `status-bar`), dark and light, Native AOT build, English, seed `m6`. Every capture is now the
  main window at 1440 x 900: drop the `-Window` arguments and the `front:<title>` steps. The tools are reached through the folder context
  menu as before (it shows the view in place); to go back to the tree in a multi-step flow (`monitors`, `status-bar`) invoke
  `Collections` (the Collections rail entry; its name is `Strings.CollectionsTitle`) instead of `front:`. Use the rail AutomationIds
  where a flow only needs to show a tool (the status bar item named "Runner" has the same name as the rail entry: never invoke by
  that name). Keep the retries.
- Add one screen `rail` (`-Crop` of the rail and sidebar edge, Runner selected, both themes): compare with `MainWindow.dc.html` (rail).
  Eighteen PNGs in total, each under 500 KB.
- `docs/screenshots/m6/README.md`: describe the captures as views of the main window; remove the text about tool window sizes and
  `-Window`/`front:` (keep them in `Capture-WinUI.ps1`, other scripts may use them); re-check D1 to D6 on the new captures and mark each
  "still open", "fixed" or "no longer applies"; add the `rail` row; add a check at the minimum window size (900 x 600: each tool shown once,
  nothing clipped) recorded as PASS/FAIL in the README (no PNG needed).
- Fix the **small** visual defects found (alignment, spacing, a clipped column) in this card; record bigger ones as follow-ups in the README.

## Out of scope

Avalonia screenshots; redesigns; a dedicated load-test icon.

## Acceptance criteria

- [ ] Eighteen PNGs under `docs/screenshots/m6/` from the Native AOT build, each under 500 KB; the capture script changes are committed.
- [ ] `docs/screenshots/m6/README.md` matches the new layout and lists the differences and the D1 to D6 status.
- [ ] No process left running (the published exe, the loopback stub and any mock server are stopped); 0 warnings, all tests green.

## Suggested commit message

`docs: recapture the milestone 6 WinUI views in the main window`
