# M2-65: Avalonia command palette

Status: todo
Platform: unix
Depends on: M2-63, M2-59

## Goal

Same as M2-64, in `Yaapic.App.Avalonia`; read M2-64 first for behaviour and texts.

## Design decisions

- `Views/CommandPaletteView.axaml` (compiled bindings, `x:DataType` = `CommandPaletteViewModel`) as the last child of the
  `MainWindow` root `Panel` (overlay, `IsVisible` = `IsOpen`, scrim closes on pointer press). `TextBox` bound to `Query`,
  `ListBox` of `Results` with `SelectedIndex` two-way; row template with title, subtitle, category chip and shortcut.
  Matched characters are highlighted by building `Run`s in code-behind (`TextBlock.Inlines`) from `TitleMatches` when
  the row is realised; no reflection-based converters.
- Focus on open and return on close as in M2-64. Keys inside: Up/Down, Enter, Esc; click executes.
- Window key bindings: Ctrl+K / Ctrl+Shift+P (Cmd+K / Cmd+Shift+P on macOS) -> `OpenPaletteCommand`; Alt+Down / Alt+Up ->
  next/previous tab; `EnvironmentsRequested` opens `EnvironmentsWindow`. On macOS the shortcut text of each row shows `⌘`
  for `Ctrl` and `⌥` for `Alt` (a small helper in `Presentation/`, with a unit-free pure function).

## Out of scope

Same as M2-64.

## Acceptance criteria

- [ ] AOT publish 0 IL2xxx/IL3xxx warnings.
- [ ] Manual smoke: same checks as M2-64 on the host OS.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add command palette`
