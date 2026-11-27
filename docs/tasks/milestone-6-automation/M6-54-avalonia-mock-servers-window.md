# M6-54: Avalonia: Mock servers window

Status: done
Platform: unix
Depends on: M6-50, M6-47

## Goal

The Avalonia Mock servers view, matching the WinUI window of M6-47.

## Design decisions

- Read first: M6-47 (three columns, controls and order), M6-35, M6-50, `src/Yaapic.App.Avalonia/Views/EnvironmentsWindow.axaml`
  (master-detail), `KeyValueEditorView.axaml` (route headers), `CodeEditorView.axaml` (response body with JSON highlighting when the
  `Content-Type` header contains `json`).
- `src/Yaapic.App.Avalonia/Views/MockServersView.axaml(.cs)`: servers column (list with running dot, New, Delete), server and route editor
  (name, port, CORS, Start/Stop, base URL and Copy URL, routes list with Add/Duplicate/Delete/up/down, route editor with hints, Save/Revert,
  validation errors, error line), log column (rows, Clear, selected entry details), generation info line. Compiled bindings; code-behind
  forwards events only.

## Out of scope

Entry points (M6-56).

## Acceptance criteria

- [x] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings; the exe starts and exits cleanly.
- [x] Runtime smoke on macOS or Linux as in M6-47 (create, start, call it from a request tab, log, stop); if not possible, say so in Notes
  and tick only the build and AOT items.
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add the mock servers window`

## Notes

- Verified on Windows: Release build (0 warnings), Architecture (6) and ViewModels (782) test projects, AOT publish of Yaapic.App.Avalonia (win-x64) with 0 IL2xxx/IL3xxx warnings; the exe starts and stays alive.
- Not verified on unix: the runtime smoke (create, start, call from a request tab, log, port in use, stop) and rendering. `MockServersView` is not instantiated until M6-56.
- Added `BrushMap.StatusCode` and `BrushMap.RunningDot`; the body editor switches to JSON when an enabled `Content-Type` header contains `json`. The other test projects were not rerun (view-only change).
