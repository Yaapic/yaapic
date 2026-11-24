# M6-61: WinUI visual verification of the milestone 6 views

Status: todo
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

- [ ] Sixteen PNGs (eight screens, two themes) under `docs/screenshots/m6/`, each under 500 KB, captured from the Native AOT build; the
  capture script changes are committed.
- [ ] `docs/screenshots/m6/README.md` exists and lists the differences.
- [ ] No process left running (the published exe and any server are stopped); 0 warnings, all tests green.

## Suggested commit message

`docs: add milestone 6 WinUI screenshots and visual review`
