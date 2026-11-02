# M1-52: Avalonia request tabs and empty state restyle

Status: done
Platform: unix
Depends on: M1-51

## Goal

Restyle the request tab strip after `design/mockups/MainWindow.dc.html` and `DSComposants2.dc.html` ("request tabs"),
and the "no tab open" state after `States.dc.html` and `DSComposants2.dc.html` ("empty state"). Equivalent of M1-39
(read it first for the texts and the dashed illustration paths).

## Context

The ViewModel work of M1-39 is already in `main`: `RequestEditorViewModel.DisplayName`, `MethodText`, `IsActive`,
`IsDirty` and `TabsViewModel.IsEmpty` exist; do not add them again. The Avalonia strip is a stock `TabControl` whose
header is `Title` (which embeds the dirty marker) plus a `✕` button, and a separate "+" button overlaid at the top
right.

## Design decisions

- `MainWindow.axaml`: apply `Classes="requests"` (M1-50) to the `TabControl`; remove the `Margin="0,0,40,0"` and the
  overlaid "+" button; put the add-tab `Button.icon` (plus glyph, tooltip "New tab (Ctrl+T)", `OnAddTab`) at the end of
  the strip (place it in the `TabControl` template's strip area, e.g. through a `TabStripPlacement`-aware
  `ControlTheme` footer, or in a `Grid` column next to a `ScrollViewer`-hosted strip; pick the simplest that works).
- **Header template** (`x:DataType="req:RequestEditorViewModel"`): method label (`TextBlock.method`,
  `BrushMap.MethodBrush(Method)`), `DisplayName` (`body`, ellipsis, max width ~140), then either a 6 px
  `YaapicAccentTextBrush` dot when `IsDirty` (`AutomationProperties.Name` "Unsaved changes") or a `Button.icon` 18 px
  close cross (`YaapicGlyphClose`, `OnCloseTab`); the cross is visible on the active tab and on pointer over, the dot
  only while not hovered (selector on `:pointerover`). Item `AutomationProperties.Name` = `Title`.
- **Empty state** (centred, spacing 10): the dashed logo illustration of `States.dc.html` as in M1-39 (110 x 76
  `Viewbox`, baseline `M20 72h80` 2 px `YaapicBorderOverlayBrush`, the two logo paths at scale 2.2 with
  `StrokeDashArray` `2 1.5`, three accent strokes `M92 18h10M96 12l4-4M98 24h8` in `YaapicAccentBorderBrush`); title
  "No request open." (`subtitle`); text "Create a request, or open a saved one from the list." (`body`, secondary, max
  width 320, centred); a `Button.accent` "New request" (`Requests.NewRequestCommand`) and a `Border.keycap` "Ctrl+T"
  hint (macOS: the shortcut the app really binds there).

## Out of scope

Pinned and preview (italic) tabs, overflow "+N" menu, split view, tab drag and drop, middle-click close.

## Acceptance criteria

- [x] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green; AOT publish 0 IL2xxx/IL3xxx warnings; the
      published exe starts and exits cleanly.
- [x] Manual check on the published exe: edit a request name, the dirty dot shows and turns into the cross on hover;
      the cross closes the tab and asks about unsaved changes; "+" opens a new tab; closing every tab shows the empty
      state, and "New request" leaves it.
- [x] Screenshots (M1-47 script: default, `-NoTabs` plus closing all tabs if needed, dark and light) inspected against
      `MainWindow.dc.html` and `States.dc.html`: strip height 36, method colours, 2 px Ion top line on the active
      tab, dirty dot, empty-state illustration and texts. Deviations listed in the card, PNGs not committed.

## Notes

Implemented as specified. Deviations: the add-tab button stays an overlay at the right end of the 36 px strip instead of
living in the `TabControl` template, because the template cannot reach the `OnAddTab` handler; it binds
`Requests.NewRequestCommand` (same `NewTab`). The 2 px top line needs `Margin="0,2,0,0"`: without it it is drawn 2 px
above the strip, under the title bar border (Windows, extended client area). The capture option `--open empty` (closes
the active tab) was added to the screenshot tooling, because `-NoTabs` makes the shell open a new tab. The dirty dot and
the hover/active cross are verified in the screenshots (new tab shows the dot, active tab the cross); the pointer-over
swap itself was not driven interactively.

## Suggested commit message

`feat(avalonia): restyle request tabs and empty state`
