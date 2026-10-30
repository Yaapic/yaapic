# M1-38: WinUI shell restyle: title bar, rail and sidebar

Status: done
Platform: windows
Depends on: M1-37

## Goal

Make the window chrome match `design/mockups/MainWindow.dc.html` (dark) and `MainWindowLight.dc.html` (light): a
40 px title bar with the logo and an environment pill, a 48 px navigation rail, and the 272 px request sidebar.

## Context

Today: title bar with the text "Yaapic", a 200 px environment `ComboBox`, a gear button and a "Theme" drop-down; a
260 px sidebar with a full-width "New" button, a plain filter `TextBox` and a default `ListView`. The mockup has no
theme picker in the title bar, a rail on the left, and a sidebar with a header row, a compact filter and 26 px rows.

## Design decisions

- File: `src/Yaapic.App.WinUI/MainWindow.xaml` (+ code-behind for view-only wiring). Layout columns of the body:
  rail `YaapicRailWidth` (48) | sidebar `YaapicSidebarWidth` (272) | tab area `*`. Rail and sidebar have a 1 px right
  border `YaapicBorderSubtleBrush` (DSTokens: separate zones with 1 px subtle borders, not shadows).
- **Title bar** (`YaapicTitleBarHeight`, background `YaapicSurfaceAppBrush`, bottom border subtle; still the
  `SetTitleBar` drag region; system caption buttons unchanged):
  - left (padding 12): the Yaapic symbol 18 x 18 drawn with two `Path`s copied from the mockup
    (`M5.5 4.5l6.3 8.2` stroked `YaapicTextPrimaryBrush`, and
    `M18.5 4.5l-7.4 12.8a2.6 2.6 0 0 1-2.3 1.3H5.2M7.6 16.1l-2.5 2.5 2.5 2.5` stroked `YaapicAccentTextBrush`,
    thickness 2.6, round caps and joins, `Viewbox` 24 -> 18), then "Yaapic" in `YaapicBodyStrongTextStyle`.
    Store the two geometries as `YaapicLogoStemGeometry` / `YaapicLogoArrowGeometry` strings in
    `Yaapic.Controls.WinUI.xaml` (M1-38 is their first user).
  - right, before the caption buttons (140 px reserved as today): the **environment pill** (`YaapicPillButtonStyle`,
    a `DropDownButton`): 8 px dot (`YaapicSuccessBrush` when an environment is active, `YaapicTextTertiaryBrush` for
    "No environment"), active environment name or "No environment", chevron. Its `MenuFlyout` is rebuilt in
    code-behind on `Opening`: one `RadioMenuFlyoutItem` per `Environments.Environments` (checked = active) plus
    "No environment", a separator, then "Manage environments..." opening the existing dialog. Selecting an item sets
    `Environments.ActiveEnvironment`. This replaces the `ComboBox` and the gear button. `AutomationProperties.Name`
    "Environment: {name}".
  - The command search box, notifications and avatar of the mockup are not M1 features: not shown.
- **Rail** (background `YaapicSurfaceAppBrush`, padding `0,8`, items spacing 4): 36 x 36 buttons, radius 6, 20 px
  `PathIcon`s from `Yaapic.Icons.WinUI.xaml`, each with tooltip and `AutomationProperties.Name`:
  - "Requests" (`YiCollection`): always selected in M1 (selection background `YaapicSelectionBackgroundBrush`,
    foreground `YaapicAccentTextBrush`, 3 x 16 accent bar 6 px left of the button, radius 2).
  - "Environments" (`YiEnvironment`): opens the environments dialog (same handler as the pill item); new window
    accelerator Ctrl+Shift+E does the same (tooltip "Environments (Ctrl+Shift+E)"), which also lets the M1-34 script
    capture the dialog with `-Keys '^+e'`.
  - bottom-aligned "Settings" (`YiSettings`): a `MenuFlyout` with the existing Dark / Light / System theme actions
    (moved from the title bar).
  - Unselected rail buttons use `YaapicIconButtonStyle` colours (secondary, hover overlay).
- **Sidebar** (background `YaapicSurfaceSidebarBrush`):
  - header row 40 px, padding `14,0,8,0`: "Requests" (`YaapicBodyStrongTextStyle`, fills), then a
    `YaapicIconButtonStyle` "New request" button (`Requests.NewRequestCommand`, tooltip "New request (Ctrl+T)") with a
    plus glyph `YaapicGlyphAdd` = `M12 5v14M5 12h14` added to `Yaapic.Controls.WinUI.xaml`.
  - filter: padding `10,0,10,8`; a 28 px `TextBox` (radius `YaapicRadiusControl`, `YaapicSurfaceSunkenBrush`) with a
    14 px search `PathIcon` (`YiSearch`) at the left inside the box (a `Grid` overlay with `Padding` on the `TextBox`
    is fine), placeholder "Filter", a `Ctrl+Shift+F` keycap hint at the right in `YaapicCodeSmallTextStyle` tertiary,
    and a new window accelerator Ctrl+Shift+F that focuses it.
  - list: `ListView` with `ItemContainerStyle="{StaticResource YaapicListRowStyle}"` (26 px rows); item template:
    method label (`YaapicMethodTextStyle`, width 34, `BrushMap.Method`) + name (`YaapicBodyTextStyle`, ellipsis).
    `SelectionMode="Single"` and the selection follows the active tab: code-behind selects the item whose `Id` equals
    the active tab's `Id` when `Tabs.ActiveTab` changes (or clears it); `ItemClick` still opens the request; a
    selection change made by code never opens anything. Context menu "Delete" unchanged.
  - empty list (no saved request, filter empty): centered `YaapicBodySmallTextStyle` text "No saved requests yet."
    and "Press Ctrl+S in a tab to save it here."; with a filter and no match: "No request matches '{filter}'."
    (two `VisibilityMap` helpers if needed, tested only through the build since they are trivial).
- `Tabs.LastError` / `Environments.LastError` `InfoBar`s stay under the title bar, full width, styled by M1-37.

## Out of scope

Collections tree, favourites, folders, import, git status footer, status bar, command palette, resizable sidebar,
compact mode (`design/mockups` 18b), tab strip and empty tab state (M1-39), Mica tuning.

## Acceptance criteria

- [x] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green.
- [x] AOT publish 0 IL2xxx/IL3xxx warnings; published exe starts and exits cleanly.
- [ ] Manual check (not done interactively: only start, request/response and the capture were exercised; see Notes): the pill switches environments and opens the dialog; the rail Settings menu switches themes;
      Ctrl+Shift+F focuses the filter; clicking a saved request opens it and its row is selected.
- [x] Screenshots (M1-34 script) dark and light inspected against `MainWindow.dc.html` / `MainWindowLight.dc.html`
      (title bar, rail, sidebar only); differences listed in the card's Notes, not committed.

## Notes

- Deviations: the logo paths are literal `Data` strings in `MainWindow.xaml` (no `YaapicLogo*Geometry` resources: a
  `Path.Data` cannot bind a string resource, and WinUI XAML has no `StreamGeometry`). Column widths and the title bar
  height use literals 48 / 272 / 40 (a `{StaticResource}` x:Double on `ColumnDefinition.Width` crashes the XAML load).
  Icons come from the FluentIcons.WinUI package (`FluentIcon`, Regular variant; user decision, Lucide SVG to complement it if icons are missing) because `PathIcon` fills the stroke-only `Yi*`
  geometries. The pill is a `Button` with a flyout (not a `DropDownButton`, whose style the pill style cannot base on).
  `YaapicListRowStyle` lost its 2 px selection bar: wrapping `ListViewItemPresenter` in a `Grid` crashes the XAML load,
  so the stock 16 px selection pill is enabled instead (not visible in the screenshots, only the row tint is).
- Screens checked (AOT, dark and light, `-Send`): title bar, logo, pill, rail with selected bar, sidebar header, filter
  with search icon and keycap hint, 26 px rows with the selected row tinted, response received. NOT checked on screen:
  the pill and Settings flyouts, the environments dialog, the empty / no-match list texts, Ctrl+Shift+E / Ctrl+Shift+F.
- Remaining gaps against the mockup: the filter box and request controls keep the thick Fluent border; rail icons do
  not change colour on hover (fixed stroke brush); no selection pill visible on the list row; the title bar still shows
  the OS caption buttons; tab strip, editor and response are untouched (M1-39 onward).

## Suggested commit message

`feat(winui): restyle title bar, navigation rail and request sidebar`
