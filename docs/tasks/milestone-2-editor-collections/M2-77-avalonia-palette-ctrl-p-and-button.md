# M2-77: Avalonia palette Ctrl+P shortcut and button

Status: done
Platform: unix
Depends on: M2-65

## Goal

Make the command palette discoverable: Ctrl+P opens it and a visible button in the title bar or rail opens it too.

## Design decisions

- Add a `KeyBinding` `Ctrl+P` (Cmd+P on macOS) in `MainWindow.axaml.cs` next to the existing ones.
- Add an icon button (design tokens, `AutomationProperties.Name` "Command palette") in the title bar or rail that runs `OpenPaletteCommand`; the `ShortcutText` helper formats the tooltip.
- Update the shortcut text shown for the palette command in `CommandsPaletteSource` (shared with M2-76; do it once, in whichever card lands first).

## Out of scope

New palette behaviour, other shortcuts, remapping UI.

## Acceptance criteria

- [x] Ctrl+P (Cmd+P on macOS) and the button open the palette; Ctrl+K and Ctrl+Shift+P keep working.
- [x] The button tooltip shows the shortcut; the `Commands` palette entry for the palette shows `Ctrl+P`.
- [x] 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add palette ctrl+p shortcut and button`
