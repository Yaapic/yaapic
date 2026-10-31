# M1-30: Avalonia response viewer

Status: todo
Platform: unix
Depends on: M1-29

## Goal

Same response viewer as M1-26, in `Yaapic.App.Avalonia`.

## Design decisions

- Behaviour, texts, states and colours are those of M1-26; read it first.
- `Views/ResponsePanelView.axaml`, compiled bindings, `x:DataType` = `ResponsePanelViewModel`.
- Control mapping: `InfoBar` -> a `Border` with the matching `Yaapic*BackgroundBrush` and a `TextBlock`;
  `ProgressRing` -> `ProgressBar IsIndeterminate="True"`; body viewer -> read-only `TextBox` (`IsReadOnly`, monospace,
  `TextWrapping="NoWrap"`); Pretty/Raw -> two `RadioButton`s; Body/Headers -> `TabControl`; headers -> `ItemsControl`
  with a two-column `Grid` template (`SelectableTextBlock`).

## Out of scope

Same as M1-26.

## Acceptance criteria

- [ ] AOT publish 0 IL2xxx/IL3xxx warnings.
- [ ] Manual smoke: same checks as M1-26.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add response viewer`
