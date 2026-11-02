# M1-52: Avalonia request tabs and empty state restyle

Status: todo
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

- [ ] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green; AOT publish 0 IL2xxx/IL3xxx warnings; the
      published exe starts and exits cleanly.
- [ ] Manual check on the published exe: edit a request name, the dirty dot shows and turns into the cross on hover;
      the cross closes the tab and asks about unsaved changes; "+" opens a new tab; closing every tab shows the empty
      state, and "New request" leaves it.
- [ ] Screenshots (M1-47 script: default, `-NoTabs` plus closing all tabs if needed, dark and light) inspected against
      `MainWindow.dc.html` and `States.dc.html`: strip height 36, method colours, 2 px Ion top line on the active
      tab, dirty dot, empty-state illustration and texts. Deviations listed in the card, PNGs not committed.

## Suggested commit message

`feat(avalonia): restyle request tabs and empty state`
