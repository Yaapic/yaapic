# M6-47: WinUI: Mock servers window

Status: done
Platform: windows
Depends on: M6-43

## Goal

The Mock servers window bound to `MockServersViewModel` (ADR 0020, "UI").

## Design decisions

- Read first: ADR 0020, M6-35 (members, strings), M6-43, `Views/EnvironmentsDialog.xaml` (master-detail layout, the `EnvVars.dc.html`
  mockup it follows), `Views/KeyValueEditorView.xaml` (reuse it for route headers), `Views/CodeEditorView.xaml` (reuse the code editor for
  the response body, JSON highlighting when the `Content-Type` header contains `json`, plain text otherwise).
- `Views/MockServersView.xaml(.cs)`, three columns:
  1. Servers (240 px): list (name, port, a running dot in the ok brush), **New mock server**, **Delete**.
  2. Selected server (fills): name `TextBox`, port `NumberBox`, CORS `CheckBox`, **Start**/**Stop**, base URL text with **Copy URL**, then the
     routes list (enabled `CheckBox`, method badge, path, status) with Add / Duplicate / Delete / up / down buttons, then the selected route
     editor: name, method `ComboBox` (`MockMethods.All`), path with `Strings.MockPathHint`, status `NumberBox`, delay `NumberBox`, headers
     (`KeyValueEditorView`), body (code editor) with `Strings.MockTemplateHint`, `IssueText`; **Save** / **Revert** and `ValidationErrors` at
     the bottom; `Error` line.
  3. Log (320 px): newest-first rows (time, method, target, status coloured by class, route or "no match", duration), **Clear**, and the
     selected entry's headers and body preview below.
  `GenerationMessage` shows as an info bar when set.
- Lists through `ListMap`. Code-behind forwards events only.

## Out of scope

Entry points (M6-49).

## Acceptance criteria

- [x] AOT publish of `Yaapic.App.WinUI`: 0 IL2xxx/IL3xxx warnings; the exe starts.
- [x] Manual smoke: create a server, add `GET /users/:id` returning `{"id":"{{params.id}}"}`, save, start, send `GET http://127.0.0.1:3000/users/7`
  from a Yaapic request tab (response `{"id":"7"}`), the log shows the request with its route; a port in use shows the error; stop frees the
  port; generate from a folder (through the debug call or M6-49). Record anything not run in Notes.
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add the mock servers window`

## Notes

- Verified: Release build (0 warnings), full test run green, AOT publish with 0 IL2xxx/IL3xxx warnings, the published exe starts and stays alive.
- Not run: the manual smoke (create/start a server, request from a tab, log, port in use, stop, generate from a folder). `MockServersView` is not
  instantiated anywhere until M6-49; check it then (and in M6-61).
- New string `mock.revert`; `BrushMap.StatusCode` (status colour by class) and `BrushMap.RunningDot`. The body editor switches to JSON when an enabled
  `Content-Type` header contains `json`.
