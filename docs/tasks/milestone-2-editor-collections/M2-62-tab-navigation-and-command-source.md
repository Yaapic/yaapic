# M2-62: Tab navigation and the commands palette source

Status: done
Platform: any
Depends on: M2-61, M2-57, M2-33

## Goal

The fixed palette commands, and the tab navigation they need.

## Design decisions

- `TabsViewModel` gains `[RelayCommand] NextTab()` and `PreviousTab()`: activate the next/previous tab, wrapping around;
  no-op with fewer than two tabs.
- `ShellViewModel` gains `event EventHandler? EnvironmentsRequested` and `void RequestEnvironments()` (raises it; the
  views open their environments dialog/window). The palette property and its open command come in M2-63.
- `CommandsPaletteSource : IPaletteSource` in `Yaapic.ViewModels.Palette`, constructor `(ShellViewModel shell)`, category
  "Command", with these items (`Id` = `command:<slug>`; `Shortcut` text uses `Ctrl` and the platform views substitute the
  macOS symbols):

  | Title | Shortcut | Action | Only listed when |
  |---|---|---|---|
  | New request | Ctrl+T | `Tabs.NewTab()` | always |
  | Save request | Ctrl+S | `Tabs.SaveActiveCommand` | a tab is active |
  | Close tab | Ctrl+W | `Tabs.CloseActiveCommand` | a tab is active |
  | Send request | Ctrl+Enter | active tab `SendCommand` | a tab is active and it can execute |
  | Cancel request | Esc | active tab `CancelCommand` | the active tab is sending |
  | Next tab | Alt+Down | `Tabs.NextTabCommand` | two or more tabs |
  | Previous tab | Alt+Up | `Tabs.PreviousTabCommand` | two or more tabs |
  | New collection | none | `Collections.NewCollectionCommand` | always |
  | Manage environments | Ctrl+Shift+E | `RequestEnvironments()` | always |
  | Show collections | none | `ShowCollectionsCommand` | always |
  | Show history | none | `ShowHistoryCommand` | always |
  | Clear history | none | `History.ClearCommand` | history not empty |

  The list is built in `GetItems()` so the "only listed when" conditions are evaluated at open time.

## Out of scope

Request, environment and tab sources and the shell property (M2-63), the views.

## Acceptance criteria

- [x] Tests in `Yaapic.ViewModels.Tests`: next/previous wrap and no-op cases; `RequestEnvironments` raises the event; the
      source lists exactly the expected titles for an empty shell, for a shell with one tab, with two tabs, while sending,
      and with history entries; executing "New request" adds a tab, "Show history" switches the mode, "Manage
      environments" raises the event.
- [x] 0 warnings.

## Suggested commit message

`feat(viewmodels): add tab navigation and the palette command source`
