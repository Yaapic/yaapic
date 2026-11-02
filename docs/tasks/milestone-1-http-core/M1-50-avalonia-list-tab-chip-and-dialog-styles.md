# M1-50: Avalonia control styles: lists, tabs, chips, feedback and dialogs

Status: todo
Platform: unix
Depends on: M1-49

## Goal

Add the remaining reusable Avalonia styles the screens need, from `design/mockups/DSComposants2.dc.html`: list rows,
panel tabs, segmented control, status chip, environment pill, count badge, keycap, progress, skeleton and the dialog
window look. Equivalent of M1-37 (read it first); the colour tokens it added (`color.*.border`, `color.surface.overlay`,
`color.border.overlay`, `color.overlay.*`) already exist.

## Design decisions

- New file `src/Yaapic.App.Avalonia/Styles/Yaapic.Navigation.axaml` (`Styles`, included in `App.axaml` after the
  controls file). Colours only from generated keys.
- **List rows**: `ListBoxItem` style (applies to every `ListBox`): `MinHeight` 26, padding `8,0,10,0`, corner radius 0,
  hover `YaapicOverlayHoverBrush`, pressed `YaapicOverlayPressedBrush`, selected `YaapicSelectionBackgroundBrush` with
  a 2 px `YaapicAccentFillBrush` bar at the left edge (a `Border` in the template, mockup `inset 2px 0 0 #1FC7D4`).
  Class `ListBoxItem.roomy` raises the height to 32 (environments list, M1-56).
- **Panel tabs**: class `TabControl.panel` with `TabItem` height 32, transparent background, secondary foreground,
  selected foreground primary with a 2 px `YaapicAccentFillBrush` underline, spacing 18, no top strip background, a 1
  px `YaapicBorderSubtleBrush` line under the strip, `bodySmall`-sized text. Count badges inside a header use
  `TextBlock.badge` (tertiary; accent text on the selected tab through a selector `TabItem:selected TextBlock.badge`).
- **Request tabs**: class `TabControl.requests` (M1-52 applies it): strip 36 px high on `YaapicSurfaceSidebarBrush`,
  square tabs with `12,0,10,0` padding, max width 210, 1 px `YaapicBorderSubtleBrush` right border, selected tab on
  `YaapicSurfaceBaseBrush` with a 2 px `YaapicAccentFillBrush` line at the top, 1 px subtle bottom border under the
  strip.
- **Segmented control**: class `ListBox.segmented` (horizontal `StackPanel` items panel): container sunken background,
  `YaapicBorderDefaultBrush` border, radius 6, padding 2; items padding `10,0`, 12 px text, selected item
  `YaapicSurfaceElevatedBrush` with a 1 px `YaapicBorderDefaultBrush` border and radius 4, unselected foreground
  secondary. Used for Pretty/Raw (M1-55) and the body kind bar (M1-54).
- **Chip, badge, keycap, pill, skeleton** as `Border` classes: `Border.chip` (radius 4, padding `7,1`, text from
  `TextBlock.code` SemiBold; background and foreground still come from `ResponseMap.StatusBackground` /
  `StatusForeground`), `Border.keycap` (1 px `YaapicBorderDefaultBrush` border, 2 px bottom border, radius 4, padding
  `6,1`, text `codeSmall`; M1-52 and M1-55 reuse it), `Button.pill` (height 28, `YaapicRadiusFull`, 1 px
  `YaapicBorderDefaultBrush` border, transparent background, padding `10,0`; content: 8 px `Ellipse` + name + chevron
  icon), `Border.skeleton` (height 10, radius 3, `YaapicSurfaceRaisedBrush`).
- **Progress**: `ProgressBar` height 2, `MinHeight` 2, foreground `YaapicAccentFillBrush`, track
  `YaapicBorderSubtleBrush`; `ProgressRing`-like usage is not needed.
- **Dialogs**: style for the small app windows (`PromptWindow`, the unsaved-changes and delete prompts of
  `AvaloniaDialogService`) as a `Window.dialog` class: `YaapicSurfaceOverlayBrush` body, 20 px title (`title` class),
  secondary body text, a command area on `YaapicSurfaceRaisedBrush` with a 1 px `YaapicBorderDefaultBrush` top border
  and right-aligned buttons of equal width, `MinWidth` 400, no resize, centred on the owner. The unsaved prompt keeps
  "Save" as `Button.accent`; the delete prompt uses `Button.danger` for "Delete" with Cancel as default. Body copy in
  English per `design/mockups/Voix.dc.html` (same texts as M1-37: unsaved "Your changes will be lost if you don't
  save them.", delete title "Delete '{name}'?" and body "The request file will be deleted from the workspace.").
  `IDialogService` stays unchanged; update `Services/AvaloniaDialogService.cs` and `Views/PromptWindow.axaml` only for
  the look and copy.
- **Warning and error strips**: `Border.infobar` (radius 6, padding `10,5`, 1 px border) with severity classes `.info`,
  `.success`, `.warning`, `.danger` using the matching `*Background` / `*Border` / foreground brushes. Used for
  `Tabs.LastError`, the response warning and failures (M1-51, M1-55).

## Out of scope

Applying the styles to the screens (M1-51 onward), toasts, notifications, tooltips with shortcut hints, native menu bar.

## Acceptance criteria

- [ ] No `#` colour literal in `Styles/`; `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green.
- [ ] AOT publish 0 IL2xxx/IL3xxx warnings; published exe starts and exits cleanly.
- [ ] Manual check on the published exe: close a dirty tab, the unsaved dialog shows the overlay surface, 20 px title
      and a raised footer with the accent Save; delete a request from the sidebar, Delete is the danger button.
      Screenshot (`--open unsaved`, dark and light) inspected against `DSComposants2.dc.html` ("ContentDialog"),
      deviations noted in the card, PNGs not committed. The other classes have no screen yet: they are checked by
      M1-51 onward.

## Suggested commit message

`feat(avalonia): add list, tab, chip, feedback and dialog styles`
