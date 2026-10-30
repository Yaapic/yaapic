# M1-37: WinUI control styles: lists, tabs, chips, feedback and dialogs

Status: todo
Platform: windows
Depends on: M1-36

## Goal

Add the remaining reusable styles the screens need, from `design/mockups/DSComposants2.dc.html`: list rows, panel
tabs, segmented control, status chip, environment pill, count badge, InfoBar, progress, ContentDialog and flyouts.
Also restyle the two app dialogs (unsaved changes, delete request), which only need the dialog style.

## Design decisions

- Everything goes in `src/Yaapic.App.WinUI/Themes/Yaapic.Controls.WinUI.xaml` (M1-36). Colours only from generated
  keys; new colour needs become tokens with `winuiAliases` or new XAML keys, regenerated with the M1-33 command.
- **New tokens** (dark / light / HC): `color.info.border` `#959BFF4D` / `#4A4FD04D` / HC of `color.info`;
  `color.success.border` `#3FD68A4D` / `#157F4A4D`; `color.warning.border` `#F2B84B4D` / `#8A5A004D`;
  `color.surface.overlay` (flyouts, dialogs, tooltips) `#282C33` / `#FFFFFF`, with HC = HC of `color.surface.elevated`;
  `color.border.overlay` `#454B54` / `#B4BBC5`, HC = HC of `color.border.default`. XAML keys
  `YaapicInfoBorderBrush`, `YaapicSuccessBorderBrush`, `YaapicWarningBorderBrush`, `YaapicSurfaceOverlayBrush`,
  `YaapicBorderOverlayBrush` (danger border exists since M1-36).
- **Aliases** to add: `color.surface.overlay` -> `FlyoutPresenterBackground`, `MenuFlyoutPresenterBackground`,
  `ComboBoxDropDownBackground`, `ToolTipBackground`, `ContentDialogBackground`; `color.border.overlay` ->
  `FlyoutBorderThemeBrush`, `MenuFlyoutPresenterBorderBrush`, `ComboBoxDropDownBorderBrush`, `ToolTipBorderBrush`,
  `ContentDialogBorderBrush`. The mockup dialog has a body on the overlay surface and a command area (buttons) on
  `#21252A` with a 1 px top border: find in the WinAppSDK `generic.xaml` the keys the `ContentDialog` template uses
  for the command area background and its separator, and alias them to `color.surface.raised` and
  `color.border.default`. `color.overlay.hover` -> `ListViewItemBackgroundPointerOver`, `MenuFlyoutItemBackgroundPointerOver`;
  `color.selection.background` -> `ListViewItemBackgroundSelected`, `ListViewItemBackgroundSelectedPointerOver`;
  `color.overlay.pressed` -> `ListViewItemBackgroundPressed`.
- **Styles** (all `x:Key`, documented with an XML comment line each):
  - `YaapicListRowStyle` (`ListViewItem`, `BasedOn="{StaticResource DefaultListViewItemStyle}"`): `MinHeight` 26,
    `Padding` `8,0,10,0`, `HorizontalContentAlignment` Stretch, corner radius 0, selection indicator: a 2 px
    accent bar at the left edge (mockup `inset 2px 0 0 #1FC7D4`). Use the WinUI `ListViewItem` selection indicator
    resources (`ListViewItemSelectionIndicatorBrush` -> accent fill alias, `ListViewItemSelectionIndicatorCornerRadius`
    0, indicator visual enabled) rather than a template copy, if they give a full-height 2 px bar; otherwise copy the
    template.
  - `YaapicPanelTabStyle` for `SelectorBarItem`: height 32, no pill background, foreground secondary, selected
    foreground primary with a 2 px accent underline (mockup `inset 0 -2px 0 #1FC7D4`), spacing 18 between items. If
    `SelectorBarItem` cannot be restyled this way without a template copy, copy its template.
  - `YaapicSegmentedStyle`: a `ListView` horizontal style (`ItemsPanel` horizontal `StackPanel`) used as a segmented
    control: container `Border` sunken background, `YaapicBorderDefaultBrush` border, radius 6, padding 2; items with
    padding `10,0`, font size 12 (`YaapicFontSizeBodySm`), selected item background `YaapicSurfaceElevatedBrush`
    plus 1 px `YaapicBorderDefaultBrush` border, radius 4, unselected foreground secondary. Pretty/Raw (M1-42) uses it.
  - `YaapicChipBorderStyle` (`Border`): radius 4, padding `7,1`; text inside uses a new `YaapicChipTextStyle`
    (mono 12 SemiBold). Colour pairs (background / foreground) stay in `BrushMap.StatusBackground` /
    `StatusForeground`.
  - `YaapicPillButtonStyle` (`Button` or `DropDownButton`): height 28, corner radius `YaapicRadiusFull`, 1 px `YaapicBorderDefaultBrush` border, transparent background, padding `10,0`; content: an
    8 px `Ellipse` dot + name + chevron (mockup environment pill).
  - `YaapicCountBadgeTextStyle` = `YaapicBadgeTextStyle` (M1-35) with tertiary foreground; active tab variant uses the
    accent text brush.
  - InfoBar: through aliases only (`SystemFillColor*` from M1-33); add `InfoBarErrorSeverityBorderBrush`-style keys if
    they exist in `generic.xaml` (map to the border tokens above) and set `InfoBar` corner radius 6 and padding
    `10,5` with an implicit style.
  - Progress: implicit `ProgressBar` style with height 2, `MinHeight` 2, foreground accent fill, background
    `YaapicBorderSubtleBrush`; implicit `ProgressRing` foreground accent fill.
  - `YaapicSkeletonBarStyle` (`Border`, height 10, radius 3, background `YaapicSurfaceRaisedBrush`).
  - Dialog: implicit `ContentDialog` style: corner radius `OverlayCornerRadius` (8), title 20/28 SemiBold
    (`ContentDialogTitleFontSize` or a title template), body text secondary, buttons equal width (default behaviour),
    `MinWidth` 400. A shadow (`elevation.3`) is left to the platform.
- **App dialogs** (`Services/WinUIDialogService.cs`): unsaved prompt keeps "Save" primary (`AccentButtonStyle`),
  "Don't save" and "Cancel" secondary; delete prompt "Delete" uses `PrimaryButtonStyle = YaapicDangerButtonStyle` and
  "Cancel" secondary, default button Cancel. Body text in English following `design/mockups/Voix.dc.html` (what
  happens, then the consequence), e.g. delete: title "Delete '{name}'?", body "The request file will be deleted from
  the workspace." Keep the `IDialogService` contract unchanged.

## Out of scope

Applying list/tab/chip styles to the screens (M1-38 onward), toasts, notifications, context menu redesign beyond the
flyout colours, tooltips with shortcut hints.

## Acceptance criteria

- [ ] No `#` colour literal in `Themes/`; snapshot tests green after regeneration.
- [ ] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green.
- [ ] AOT publish 0 IL2xxx/IL3xxx warnings; published exe starts and exits cleanly.
- [ ] Manual check on the published exe: close a dirty tab, the dialog shows the Yaapic surface, 20 px title and a
      raised footer; delete a request from the sidebar, the Delete button is the danger style. Screenshot (M1-34
      script, or a manual capture) inspected for dark and light, not committed.

## Suggested commit message

`feat(winui): add list, tab, chip, feedback and dialog styles`
