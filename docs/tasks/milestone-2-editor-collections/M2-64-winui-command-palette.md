# M2-64: WinUI command palette

Status: todo
Platform: windows
Depends on: M2-63, M2-58

## Goal

Ctrl+K opens a command palette over the WinUI window.

## Design decisions

- No dedicated mockup exists: follow `design/mockups/DSFondations.dc.html` (principle 03: "Ctrl+K opens everything",
  Alt+Up/Down switches tabs), the list rows and dialog of `DSComposants2.dc.html` and the elevation/radius tokens.
- `Views/CommandPaletteView.xaml` (`UserControl`, `ViewModel` of type `CommandPaletteViewModel`) placed as the last child
  of the window root `Grid` (an overlay, **not** a `ContentDialog`, so it works while no other dialog is open and does
  not fight the one-dialog rule). `Visibility` from `IsOpen`; a scrim `Border` closes it on click. Card 560 px wide, top
  aligned at 15 % of the height: a borderless `TextBox` bound two-way to `Query` (`PropertyChanged`, placeholder "Type a
  command or search requests"), then a `ListView` of `Results` with single selection synced to `SelectedIndex`. Row:
  title (matched characters in the accent colour: build `Run`s in code-behind from `TitleMatches` in
  `ContainerContentChanging`; plain `x:Bind` for the rest), subtitle in the secondary text colour, category on the right
  as a small chip, `Shortcut` as a keycap-style text. Empty text "No matching commands" when `IsEmpty`.
- Focus: when it opens the `TextBox` takes focus and selects its text; when it closes, focus returns to the previously
  focused element (remember it on open).
- Keys inside the palette: Up/Down -> `MovePreviousCommand`/`MoveNextCommand`, Enter -> `ExecuteSelectedCommand`, Esc ->
  `CloseCommand`; mouse click on a row executes it (`ExecuteAsync`).
- Window accelerators (in `MainWindow`): Ctrl+K and Ctrl+Shift+P -> `ViewModel.OpenPaletteCommand`; Alt+Down / Alt+Up ->
  `Tabs.NextTabCommand` / `PreviousTabCommand`; `ViewModel.EnvironmentsRequested` opens the environments dialog (the same
  code as the existing Ctrl+Shift+E accelerator).
- `Palette.LastError` shows in the existing error `InfoBar`.

## Out of scope

Palette history persistence, icons per command, a settings page.

## Acceptance criteria

- [ ] AOT publish 0 IL2xxx/IL3xxx warnings.
- [ ] Manual smoke on the published exe: Ctrl+K opens, typing `new` lists "New request" first, Enter runs it and closes;
      typing part of a saved request name opens it in a tab; "Use environment: Local" switches the environment selector;
      "Manage environments" opens the dialog; Esc closes and the focus returns; Alt+Up/Down switch tabs.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add command palette`
