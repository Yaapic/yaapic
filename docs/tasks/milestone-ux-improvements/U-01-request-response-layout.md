# U-01: Request / response layout toggle (stacked or side by side)

Status: done
Platform: any
Depends on: none

## Goal

Like Postman, let the user show the response below the request (default) or beside it, with a toggle in the status bar (bottom right, next to the vault and Console).

## Design decisions

- `PaneLayout { Vertical, Horizontal }` in the domain and `PaneLayoutViewModel` (one shared instance, `Shell.Layout`, also set on every
  `RequestEditorViewModel.Layout` by the composition root), so all open tabs switch together.
- The choice is saved in the session file (`.yaapic/session.yaml`, key `layout`), restored at start-up; an absent or unknown value is vertical.
- Side by side keeps the name header and URL bar at the full width; the section tabs and editor take the left column, the response the
  right one, with a vertical splitter (drag, arrow keys, double-tap or Home to reset). Both columns have a 280 px minimum.
- The status bar button shows the layout it switches to; the palette has "Toggle request layout (side by side or stacked)".
- Both front-ends: WinUI (`RequestEditorView.ApplyLayout`) and Avalonia (`RequestEditorView.ApplyLayout`).

## Out of scope

Remembering the split position per layout, hiding the response panel, per-request layout.

## Acceptance criteria

- [x] Toggle switches every open tab at once and survives a restart (tests: `FileSessionStoreTests`, `ShellViewModelTests`).
- [x] Screenshots of both layouts on both front-ends in `docs/screenshots/layout`.
- [x] When the request section tabs no longer fit (narrow request column), they are replaced by a drop-down listing the same sections
      with their counts (WinUI `UpdateTabOverflow`, Avalonia `UpdateSectionsOverflow`), like Postman; it goes back to tabs when there is room.
- [x] Known limit: the response header (Body / Headers tabs next to the status chips) still overlaps in a very narrow response column.

## Suggested commit message

`feat: add the request and response layout toggle`
