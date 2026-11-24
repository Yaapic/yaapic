# M6-54: Avalonia: Mock servers window

Status: todo
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

- [ ] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings; the exe starts and exits cleanly.
- [ ] Runtime smoke on macOS or Linux as in M6-47 (create, start, call it from a request tab, log, stop); if not possible, say so in Notes
  and tick only the build and AOT items.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add the mock servers window`
