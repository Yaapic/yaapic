# M2-23: Avalonia Auth and Settings tabs

Status: todo
Platform: unix
Depends on: M2-11, M2-21, M1-57

## Goal

Same as M2-18, in `Yaapic.App.Avalonia`; read M2-18 first for behaviour and texts.

## Design decisions

- Two more `TabItem`s after Body: "Auth" (dot badge when `Auth.IsActive`) and "Settings".
- `Views/AuthEditorView.axaml` (compiled bindings, `x:DataType` = `AuthEditorViewModel`): type `ComboBox` by index, then
  the per-kind fields (`TextBox` with `PasswordChar="●"` for Password and Token), "Add to" `ComboBox`, the placeholder
  "Use {{variable}} to keep secrets out of the file", and the "This request does not use any authentication." text for
  None. Visibility through converters in `EditorMap`.
- Settings: `ToggleSwitch` "Follow redirects" and `NumericUpDown` "Timeout (seconds, 0 = default)" (minimum 0, integer
  format, value bound to `TimeoutSeconds` through a converter to `decimal?`).

## Out of scope

Same as M2-18.

## Acceptance criteria

- [ ] AOT publish 0 IL2xxx/IL3xxx warnings.
- [ ] Manual smoke: same checks as M2-18 on the host OS.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add authentication and settings tabs to the request editor`
