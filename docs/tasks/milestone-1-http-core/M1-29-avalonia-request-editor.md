# M1-29: Avalonia request editor view

Status: todo
Platform: unix
Depends on: M1-28

## Goal

Same request editor as M1-25, in `Yaapic.App.Avalonia`.

## Design decisions

- Behaviour, texts and layout are those of M1-25; read it first.
- `Views/RequestEditorView.axaml` and reusable `Views/KeyValueEditorView.axaml` (`UserControl`s, compiled bindings,
  `x:DataType` = `RequestEditorViewModel` / `KeyValueEditorViewModel`).
- Control mapping: `SelectorBar` -> `TabControl` (Params, Headers, Body); `RadioButtons` -> two `RadioButton`s in a
  `StackPanel` with the same `GroupName`; rows -> `ItemsControl` with a `DataTemplate` of `CheckBox`, two `TextBox`es
  and a delete `Button`. Body `TextBox` with `AcceptsReturn`, `TextWrapping="NoWrap"`, monospace font resource
  `YaapicMonoFont`.
- Send/Cancel swap by `IsVisible` bound to `IsSending`. Ctrl+Enter (Cmd+Enter on macOS) sends.

## Out of scope

Same as M1-25.

## Acceptance criteria

- [ ] AOT publish 0 IL2xxx/IL3xxx warnings.
- [ ] Manual smoke: same checks as M1-25.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add request editor view`
