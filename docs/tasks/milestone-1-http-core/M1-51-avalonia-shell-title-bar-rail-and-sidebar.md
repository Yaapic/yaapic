# M1-51: Avalonia shell restyle: title bar, rail and sidebar

Status: todo
Platform: unix
Depends on: M1-50

## Goal

Make the Avalonia window chrome match `design/mockups/MainWindow.dc.html` (dark) and `MainWindowLight.dc.html` (light):
a 40 px title bar with the logo and an environment pill, a 48 px navigation rail and the 272 px request sidebar.
Equivalent of M1-38 (read it first for texts, tooltips and the empty-list copy).

## Context

Today (`MainWindow.axaml`): a 48 px top row with a 200 px environment `ComboBox`, "Environments" and "Theme" text
buttons right-aligned and nothing on the left, the OS title bar above it, a 260 px sidebar with a full-width "New"
button, a plain filter `TextBox` and a default `ListBox`, and no rail.

## Design decisions

- File: `src/Yaapic.App.Avalonia/MainWindow.axaml` (+ code-behind for view-only wiring). Body columns: rail 48 |
  sidebar 272 (use `YaapicRailWidth` and `YaapicSidebarWidth`; set `ColumnDefinition.Width` from a `GridLength`
  resource if the compiled bindings allow it, else literals with a comment) | tab area `*`. Rail and sidebar have a
  1 px `YaapicBorderSubtleBrush` right border.
- **Title bar**: `ExtendClientAreaToDecorationsHint="True"` with `ExtendClientAreaChromeHints="PreferSystemChrome"` and
  `ExtendClientAreaTitleBarHeightHint` = `YaapicTitleBarHeight` (40), so the OS caption buttons stay (Windows, macOS
  traffic lights, Linux per window manager). Background `YaapicSurfaceAppBrush`, 1 px subtle bottom border. Left
  (padding 12, extra left padding on macOS for the traffic lights through `OperatingSystem.IsMacOS()` in
  code-behind): the 18 px Yaapic symbol drawn with two `Path`s with the geometries of M1-38 (stroke 2.6, round caps and
  joins, `Viewbox` 24 -> 18; stem `YaapicTextPrimaryBrush`, arrow `YaapicAccentTextBrush`) and "Yaapic" (`bodyStrong`).
  The empty areas of the bar drag the window (default with the hint). Right, before the caption buttons: the
  **environment pill** (`Button.pill`, M1-50) with an 8 px dot (`YaapicSuccessBrush` when an environment is active,
  `YaapicTextTertiaryBrush` for "No environment"), name and chevron. Its `Flyout`/`MenuFlyout` lists
  `Environments.Environments` (checked item = active) plus "No environment", a separator and "Manage environments...".
  Selecting sets `Environments.ActiveEnvironment`. `AutomationProperties.Name` "Environment: {name}". The `ComboBox`
  and the "Environments" button are removed. If `ExtendClientArea` misbehaves on a platform, fall back to the system
  title bar with the same content as a first row and note it.
- **Rail** (background `YaapicSurfaceAppBrush`, padding `0,8`, spacing 4): 36 x 36 `Button.icon`s, 20 px icons from
  `Yaapic.Icons.Avalonia.axaml`, each with tooltip and `AutomationProperties.Name`: "Requests" (`YiCollection`, always
  selected in M1: `YaapicSelectionBackgroundBrush`, `YaapicAccentTextBrush` foreground, a 3 x 16 accent bar 6 px to its
  left, radius 2), "Environments" (`YiEnvironment`, opens `EnvironmentsWindow`, tooltip "Environments (Ctrl+Shift+E)",
  new window key binding Ctrl+Shift+E; Cmd+Shift+E on macOS like the existing shortcuts) and, bottom aligned,
  "Settings" (`YiSettings`) with a `MenuFlyout` holding the Dark / Light / System actions (moved from the title bar).
- **Sidebar** (background `YaapicSurfaceSidebarBrush`): header row 40 px, padding `14,0,8,0`: "Requests" (`bodyStrong`)
  and a `Button.icon` "New request" (`Requests.NewRequestCommand`, plus glyph, tooltip "New request (Ctrl+T)");
  filter: padding `10,0,10,8`, a 28 px `TextBox` with a 14 px `YiSearch` icon inside the box, placeholder "Filter" and
  a `Ctrl+Shift+F` keycap hint (`Border.keycap`, hidden when the box has text); new window key binding Ctrl+Shift+F
  focuses it. List: `ListBox` with the 26 px row style of M1-50, item template = method label (`TextBlock.method`,
  width 34, `BrushMap.MethodBrush`) + name (`body`, ellipsis); `SelectionMode="Single"` and the selection follows the
  active tab (code-behind selects the item whose `Id` equals the active tab's `Id` when `Tabs.ActiveTab` changes, or
  clears it; a selection made by code never opens a request; clicking opens it as today, the "Tapped" handler stays);
  context menu "Delete" unchanged. Empty list: centred `bodySmall` "No saved requests yet." and "Press Ctrl+S in a tab
  to save it here."; filter without match: "No request matches '{filter}'.".
- `Tabs.LastError` strip stays under the title bar, restyled with `Border.infobar.danger`.

## Out of scope

Collections tree, history mode, command palette, resizable sidebar, tab strip and empty tab state (M1-52), menu bar,
status bar, custom (fully drawn) caption buttons.

## Acceptance criteria

- [ ] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green; AOT publish 0 IL2xxx/IL3xxx warnings.
- [ ] Manual check on the published exe (host OS): the pill switches environments and opens the dialog, the rail
      Settings menu switches themes, Ctrl+Shift+F focuses the filter, clicking a saved request opens it and its row
      is selected, the window can still be dragged by the title bar.
- [ ] Screenshots (M1-47 script, dark and light) inspected against `MainWindow.dc.html` / `MainWindowLight.dc.html`
      (title bar, rail, sidebar only): colours, 40 / 48 / 272 px sizes, logo, pill dot, selected row tint. Deviations
      listed in the card, PNGs not committed.

## Suggested commit message

`feat(avalonia): restyle title bar, navigation rail and request sidebar`
