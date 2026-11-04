# M2-76: WinUI palette Ctrl+P shortcut and button

Status: todo
Platform: windows
Depends on: M2-64

## Goal

Make the command palette discoverable: Ctrl+P opens it and a visible button in the title bar or rail opens it too.

## Design decisions

- Add a `KeyboardAccelerator` `P` + `Control` in `MainWindow.xaml` bound to `OnPaletteShortcut`, next to the existing ones.
- Add an icon button (search glyph, design tokens, accessible name "Command palette") in the title bar or rail that runs `OpenPaletteCommand`.
- Update the shortcut text shown for the palette command in `CommandsPaletteSource` (shared with M2-77; do it once, in whichever card lands first).

## Out of scope

New palette behaviour, other shortcuts, remapping UI.

## Acceptance criteria

- [ ] Ctrl+P (Cmd+P on macOS) and the button open the palette; Ctrl+K and Ctrl+Shift+P keep working.
- [ ] The button tooltip shows the shortcut; the `Commands` palette entry for the palette shows `Ctrl+P`.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add palette ctrl+p shortcut and button`
