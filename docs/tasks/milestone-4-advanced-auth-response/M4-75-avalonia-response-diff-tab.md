# M4-75: Avalonia: response Diff tab

Status: todo
Platform: unix
Depends on: M4-48, M4-61

## Goal

The Avalonia response panel has a Diff tab, matching M4-67.

## Design decisions

- Read first: M4-67 (content and behaviour are identical, including the `diffAdded` / `diffRemoved` surface tokens: if M4-67 added them, reuse them; otherwise add them in the shared token
  source and regenerate as M0-04 describes), `Views/ResponsePanelView.axaml(.cs)`, M4-48.
- New `TabItem` "Diff" with the toolbar (summary, status line, Compare-with `ComboBox`, Pin/Unpin button, `ToggleSwitch`es for ignore whitespace, ignore volatile headers, show all lines), the
  headers section and a virtualized `ItemsControl`/`ListBox` of `BodyLines` (`VirtualizingStackPanel`, fixed row height, number columns, marker column, monospace text, row background from the
  `DiffOp` through a `Presentation/DiffMap` converter class), fold rows, and the empty and unavailable states.
- No selection, no logic in code-behind, compiled bindings.

## Out of scope

Side-by-side layout, intra-line highlighting, patch export.

## Acceptance criteria

- [ ] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings; the exe starts and exits cleanly (kill it afterwards).
- [ ] Runtime smoke on macOS or Linux as in M4-67; if not possible, say so in the Notes and tick only the build and AOT items.
- [ ] Strings in EN and FR; 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add the response Diff tab`
