# M1-39: WinUI request tabs and empty state restyle

Status: done
Platform: windows
Depends on: M1-38

## Goal

Restyle the request tab strip after `design/mockups/MainWindow.dc.html` and `DSComposants2.dc.html` ("request
tabs"), and the "no tab open" state after `design/mockups/States.dc.html` and `DSComposants2.dc.html` ("empty state").

## Context

Today the WinUI `TabView` uses the stock Fluent look (rounded floating tabs, accent-less), and the header is
`RequestEditorViewModel.Title`, which embeds the dirty marker (`"Name •"`). The mockup tab: 36 px strip on the
sidebar surface, square tabs separated by 1 px subtle borders, method label in colour, name, and a dirty dot or close
cross; the active tab has the base surface and a 2 px Ion line at the top.

## Design decisions

- **ViewModel** (`src/Yaapic.ViewModels/Requests/RequestEditorViewModel.cs`): add `DisplayName` (the `Title` text
  without the dirty suffix: trimmed `Name`, or the default name when empty), raised whenever `Title` is. `Title`
  stays as is (window title and automation still use it). Tests in `tests/Yaapic.ViewModels.Tests`: `DisplayName`
  for an empty and a named request, and change notification when `Name` changes.
- **Tab strip** (`MainWindow.xaml`, `TabView`):
  - colours through generated aliases (add to the token file, regenerate): `color.surface.sidebar` ->
    `TabViewBackground`, `TabViewItemHeaderBackground`; `color.surface.base` -> `TabViewItemHeaderBackgroundSelected`;
    `color.overlay.hover` -> `TabViewItemHeaderBackgroundPointerOver`; `color.text.secondary` ->
    `TabViewItemHeaderForeground`; `color.text.primary` -> `TabViewItemHeaderForegroundSelected`,
    `TabViewItemHeaderForegroundPointerOver`; `color.border.subtle` -> `TabViewItemSeparator`. Verify the key names in
    the WinAppSDK `generic.xaml` and use the real ones.
  - metrics in `Yaapic.Controls.WinUI.xaml`: strip height `YaapicTabBarHeight` (36), tab corner radius 0, tab padding
    `12,0,10,0`, max tab width 210, a 1 px bottom border `YaapicBorderSubtleBrush` under the strip.
  - active tab top line: copy the `TabViewItem` template from `generic.xaml` into `Yaapic.Controls.WinUI.xaml` as
    `YaapicTabViewItemStyle` and add a 2 px `YaapicAccentFillBrush` rectangle at the top, visible only in the selected
    states. Do not change close-button behaviour.
  - header template (`DataTemplate x:DataType="req:RequestEditorViewModel"`): method label (`YaapicMethodTextStyle`,
    `BrushMap.Method(Method)`), `DisplayName` (`YaapicBodyTextStyle`, ellipsis), then a 6 px `YaapicAccentTextBrush`
    dot when `IsDirty` (`AutomationProperties.Name` "Unsaved changes"). `AutomationProperties.Name` of the item is
    `Title`.
  - the add-tab button: `YaapicIconButtonStyle` look with `YaapicGlyphAdd`, tooltip "New tab (Ctrl+T)".
- **Empty state** (no tab open), centred, spacing 10:
  - the dashed logo illustration from `States.dc.html` ("VIDE · collections"): a 110 x 76 `Viewbox` with the baseline
    `M20 72h80` (2 px, `YaapicBorderOverlayBrush`), the two logo paths scaled 2.2 with `StrokeDashArray` `2 1.5`
    (`YaapicBorderOverlayBrush`) and the three accent strokes `M92 18h10M96 12l4-4M98 24h8` in `YaapicAccentBorderBrush`
    (`#1FC7D4` dark, `#0A7C86` light, as in the mockup).
  - title "No request open." (`YaapicSubtitleTextStyle`), text "Create a request, or open a saved one from the list."
    (`YaapicBodyTextStyle`, secondary, max width 320, centred).
  - buttons: "New request" (primary, `Requests.NewRequestCommand`) and a keycap hint "Ctrl+T" (`YaapicCodeSmallTextStyle`
    in a 1 px `YaapicBorderDefaultBrush` border, bottom border 2 px, radius 4, padding `6,1`). Define that keycap
    look once as `YaapicKeycapBorderStyle` in `Yaapic.Controls.WinUI.xaml`; M1-41 and M1-42 reuse it.

## Out of scope

Pinned and preview (italic) tabs, overflow "+N" button, split view button, tab drag and drop, middle-click close.

## Acceptance criteria

- [x] New ViewModel tests green; `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green; snapshot tests
      green after regeneration.
- [x] AOT publish 0 IL2xxx/IL3xxx warnings; published exe starts and exits cleanly.
- [x] Screenshots (M1-34 script) dark and light inspected: method colours in tabs, active tab line, dirty dot after
      editing, empty state after closing all tabs. Differences from the mockups listed in the card's Notes.

## Notes

Implemented: `DisplayName`, `MethodText` and `IsActive` on `RequestEditorViewModel` and `IsEmpty` on `TabsViewModel`
(tests added), the `TabView*` colour aliases in the token file (regenerated), the tab strip metrics (36 px, no header
padding, max width 210, padding `12,0,10,0`), the tab header (method label, name, dirty dot), square tabs with a 2 px
Ion line on the selected one, the add-tab button, the empty state (dashed logo, texts, "New request", keycap) and
`YaapicKeycapBorderStyle`.

How it was made to work (all verified on the Native AOT exe, dark and light):

- The native fail-fast (0xC000027B) of the first attempt was a missing resource: the stock `TabViewCloseButtonStyle`
  is not reachable by key from the app, so the copied `TabViewItem` template could not resolve it (error text read from a
  non-AOT build through `Application.UnhandledException`: "Cannot find a Resource with the Name/Key
  TabViewCloseButtonStyle"). `Yaapic.Controls.WinUI.xaml` now holds copies of both styles (`YaapicTabViewCloseButtonStyle`,
  `YaapicTabViewItemStyle`, from WindowsAppSDK 1.8 `generic.xaml`). The item copy drops the rounded-tab arcs and the
  selected-tab path (square corners, no 8 px arcs), replaces the converter-based `CornerRadius` and adds the accent line.
- The add-tab button cannot be restyled by overriding `TabViewButtonStyle` (the stock template resolves its own copy
  first), so the stock button is hidden and a 36 px `YaapicIconButtonStyle` button with the "+" path and the tooltip
  "New tab (Ctrl+T)" sits in `TabStripFooter`.
- The empty state was invisible on HEAD too: x:Bind does not re-evaluate a function binding when its argument becomes
  null (the generated code only calls it when `ActiveTab` is not null). It now binds to `TabsViewModel.IsEmpty`.

Remaining differences from the mockup: the close cross is shown on every tab (the mockup shows the dirty dot or the
cross only on hover/active tabs), the 1 px separator between tabs is barely visible, no pinned / preview (italic) tabs, "+N" overflow
button or split-view button (out of scope), and the primary button of the empty state uses `AccentButtonStyle`.

## Suggested commit message

`feat(winui): restyle request tabs and empty state`
