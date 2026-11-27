# M6-61: WinUI visual verification of the milestone 6 views

Status: in progress
Platform: windows
Depends on: M6-45, M6-46, M6-47, M6-48, M6-49

## Goal

Capture the new WinUI windows in dark and light from the Native AOT build, commit the screenshots and record how they differ from the
design, as M5-68 did for milestone 5.

## Design decisions

- Same method as M5-68: `tools/screenshots/Capture-WinUI.ps1` (extend the seeded workspace and the scripted flow only as far as needed; a
  loopback server or a Yaapic mock server started by the script provides the responses; **no real secret**, nothing leaves the machine),
  1440 x 900 (tool windows captured at their default 1100 x 720), Native AOT publish output, PNG under `docs/screenshots/m6/` as
  `<screen>-<theme>.png`, each under 500 KB.

  | Screen | Compare with |
  |---|---|
  | `runner-setup` (folder loaded, a CSV data file with its preview) | `DSComposants2.dc.html` (lists, fields) |
  | `runner-results` (a finished run with passed, failed and skipped rows, one selected) | `ResponseStates.dc.html` (Tests tab rows) |
  | `load-test-running` (charts and tiles during a ramp-up) | `DSComposants2.dc.html` (cards) |
  | `load-test-done` (per-request table and errors) | `DSComposants2.dc.html` |
  | `mock-servers` (a running server, a route selected, log entries) | `EnvVars.dc.html` (master-detail) |
  | `monitors` (one passing and one failing monitor, history) | `States.dc.html` (failing monitor notification) |
  | `folder-menu` (context menu with the four entries) | `MainWindow.dc.html` |
  | `status-bar` (Runner progress, Mocks and Monitors items) | `MainWindow.dc.html` (footer) |

  If a mockup does not exist for a screen, say so and compare with the closest component sheet.
- Fix the **small** visual defects found (alignment, spacing, a literal colour, a clipped dialog, a list that crashes under AOT) in the same
  card; record bigger ones as follow-up notes. Commit the PNGs and `docs/screenshots/m6/README.md` listing each screenshot with its
  differences from the design.

## Out of scope

Avalonia screenshots, redesigns.

## Acceptance criteria

- [x] Sixteen PNGs (eight screens, two themes) under `docs/screenshots/m6/`, each under 500 KB, captured from the Native AOT build; the
  capture script changes are committed.
- [x] `docs/screenshots/m6/README.md` exists and lists the differences.
- [x] No process left running (the published exe and any server are stopped); 0 warnings, all tests green.

## Notes

Done from the Native AOT build, driven through UI Automation (`tools/screenshots/Capture-M6.ps1`, 16 PNGs, all under 100 KB) and
reviewed by the implementing agent only. Checks deferred by M6-43..M6-49:

- Folder context menu with the four entries: PASS (`folder-menu-*`). Palette entries *Run collection...*, *Load test...*, *Mock servers*,
  *Monitors* each open their window: PASS (checked by window title, not by screenshot).
- Runner: setup with CSV preview and a run with a passed, a failed and a skipped row, one selected: PASS (screenshots). Progress in the status bar
  while running (`Runner 9/120`): PASS. Not run: reorder, validation line, Stop, the two exports.
- Load test: confirmation dialog over the tool window (UI Automation: *Start a load test?* with *Continue*) and a run with ramp-up: PASS; the
  charts' `Points` property renders under Native AOT: PASS. Not run: Stop, export, data file.
- Mock servers: generate from a folder, start, three requests from outside, log with a selected entry: PASS after the fix below. Not run: port in use,
  copy URL, editing and saving a route.
- Monitors: two monitors (failing and passing), *Run now*, history: PASS after the fix below. Not run: the console warning, toggle, delete.
- Status bar items *Mocks: 1 running* and *Monitors: 1 failing* appear: PASS.
- Closing the main window with the Mock servers and Runner windows open ends the process (exit code 0) and the mock port stops answering: PASS.

Release build of the solution: 0 warnings; `Yaapic.ViewModels.Tests` (782) and `Yaapic.Architecture.Tests` (6) green; the other test projects
were not touched by this card and not rerun.

Bugs found and fixed here (see `docs/screenshots/m6/README.md`): Mock servers crashed the AOT app when a route was selected (`7c45c8a`), Monitors combo
boxes empty (`efcc2d4`), tool windows sized in physical pixels and Runner layout (`26ca548`), the false "scripts were not run" note of the load test
(`2a6eba1`). Follow-ups D1 to D6 are in that README.

Left for a human before this card can be `done`: a side-by-side comparison of the screenshots with the design sheets (the README says it was
not done), a designer's judgement of colours and alignment, the French locale of the four windows, and the "Not run" items above.

## Suggested commit message

`docs: add milestone 6 WinUI screenshots and visual review`
