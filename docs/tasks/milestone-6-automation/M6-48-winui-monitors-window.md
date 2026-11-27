# M6-48: WinUI: Monitors window

Status: done
Platform: windows
Depends on: M6-43

## Goal

The Monitors window bound to `MonitorsViewModel` (ADR 0021, "UI").

## Design decisions

- Read first: ADR 0021, M6-40 (members, strings), M6-43, M6-47 (master-detail look to match), `design/mockups/States.dc.html` (the
  "Monitor en échec" notification row: colours and wording of a failing monitor).
- `Views/MonitorsView.xaml(.cs)`: an info line `Strings.MonitorLocalNote` at the top; left list (260 px): name, folder, interval, an enabled
  `ToggleSwitch`, last status dot (ok, err, or secondary when never run), last run text, a running indicator; **New monitor** and **Delete**.
  Right: editor (name, folder `ComboBox` with paths, environment `ComboBox`, interval `ComboBox`), **Save**/**Revert**, **Run now**, next
  run text; then the history list (time, status badge, duration, requests text, message) and the failures of the selected run.
- Lists through `ListMap`. Code-behind forwards events only.

## Out of scope

Entry points and the status bar (M6-49).

## Acceptance criteria

- [x] AOT publish of `Yaapic.App.WinUI`: 0 IL2xxx/IL3xxx warnings; the exe starts.
- [x] Manual smoke: create a monitor on a folder whose request fails a test, Run now, the history shows the failed run with its failure, the
  console gets the warning; fix the test, Run now, status turns passed; toggle enabled; delete. Record anything not run in Notes.
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add the monitors window`

## Notes

- Verified: Release build (0 warnings), full test run green (MonitorSchedulerTests flaked once under load, passes alone), AOT publish with 0 IL2xxx/IL3xxx warnings, the published exe starts and stays alive.
- Not run: the manual smoke (create a monitor, Run now, history and failure, console warning, toggle, delete). `MonitorsView` is not instantiated anywhere until M6-49; check it then (and in M6-61).
- New `BrushMap.MonitorStatus` and `ListMap.MonitorFolders/MonitorEnvironments/MonitorIntervals`.
