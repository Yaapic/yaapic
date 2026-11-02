# M2-52: Avalonia URL variable hints

Status: todo
Platform: unix
Depends on: M2-46, M2-21

## Goal

Same as M2-49, in `Yaapic.App.Avalonia`; read M2-49 first for behaviour and texts.

## Design decisions

- In `Views/RequestEditorView.axaml`, under the URL row, an `ItemsControl` with a `WrapPanel` items panel bound to
  `UrlVariables`, `IsVisible` bound to `HasUrlVariables`; chip template (`x:DataType` = `VariableHint`) styled with the
  success and warning brushes from the generated resources; text, value trimming and tooltips as in M2-49; mapping through
  converters in `Presentation/`.

## Out of scope

Same as M2-49.

## Acceptance criteria

- [ ] AOT publish 0 IL2xxx/IL3xxx warnings.
- [ ] Manual smoke: same checks as M2-49 on the host OS.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): show URL variable hints under the URL bar`
