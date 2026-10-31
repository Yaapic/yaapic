# M1-42: WinUI response panel restyle

Status: done
Platform: windows
Depends on: M1-41

## Goal

Make `Views/ResponsePanelView.xaml` match the response section of `design/mockups/MainWindow.dc.html` and the
response states of `ResponseStates.dc.html` and `States.dc.html`: one header line with panel tabs and status, a
Pretty/Raw segmented control, a sunken code surface, and designed empty, sending and failed states.

## Design decisions

- Panel background `YaapicSurfaceBaseBrush`. Keep every existing binding and `VisibilityMap` state function; only
  the presentation changes.
- **Header** (36 px, padding `16,0,12,0`, 1 px subtle bottom border), visible in all states except Empty:
  - left: `SelectorBar` with `YaapicPanelTabStyle`: "Body", "Headers" + badge `Headers.Count` (hidden when 0).
  - right (spacing 10, `YaapicMonoFont` 12): the **status chip** (`YaapicChipBorderStyle`, background / foreground from
    `BrushMap.StatusBackground` / `StatusForeground`) containing a 12 px icon and `StatusText` (SemiBold); then
    `DurationText` and `SizeText` (secondary). Icons, stroked 3 px with the chip foreground (never colour alone,
    DSFondations principle 05): Success `M5 12.5l4.5 4.5L19 7.5`; ClientError `M12 4 21 19.5H3zM12 10v4M12 17h.01`;
    ServerError `M8.5 3.5h7l5 5v7l-5 5h-7l-5-5v-7zM9.5 9.5l5 5M14.5 9.5l-5 5`; Informational and Redirect
    `M12 3.5a8.5 8.5 0 1 0 0 17 8.5 8.5 0 0 0 0-17zM12 11v5M12 8h.01`. Store them as `YaapicGlyphStatus*` strings in
    `Yaapic.Controls.WinUI.xaml`; show the right one with four `PathIcon`s and new `VisibilityMap.WhenCategory`-style
    functions (a `string` -> `Geometry` binding is not available in compiled bindings). The chip has
    `AutomationProperties.Name` = `StatusText` and is an `AutomationProperties.LiveSetting="Polite"` live region
    (DSTokens accessibility rule).
- **Body tab** toolbar (40 px, padding `16,0,12,0`): Pretty / Raw as `YaapicSegmentedStyle` (M1-37), keeping the
  index mapping to `ViewMode`.
- **Body surface**: `YaapicSurfaceSunkenBrush`, 1 px subtle top border, padding `0,2`; the read-only body `TextBox`
  (and the binary one) borderless and transparent, `YaapicMonoFont` 12/20. M1-44 replaces it with the code editor.
- **Headers tab**: rows with line height 24, 1 px subtle bottom border, padding `14,0`; key column 170 px in
  `YaapicSyntaxKeyBrush`, value `YaapicTextPrimaryBrush`, both `YaapicMonoFont` 12, selectable, value ellipsis with
  the full value in a tooltip.
- **Empty state** (sunken background, centred, spacing 12): a 64 x 40 illustration `Path`
  `M4 30h8M16 30h14M34 30h6M44 30h16M4 20h22M30 20h10M4 10h12` (3 px, round caps, `YaapicBorderOverlayBrush`); text
  "The response will appear here." (secondary); a hint line with two keycaps (`YaapicKeycapBorderStyle` from M1-39)
  "Ctrl" + "Enter" followed by "to send" (12, tertiary).
- **Sending state**: header shows "Sending" (`YaapicBodyStrongTextStyle`) instead of the tabs and chip; under it an
  indeterminate 2 px `ProgressBar` (implicit style from M1-37); then three `YaapicSkeletonBarStyle` bars at 55 %, 80 %
  and 35 % of the width (star columns), padding `16`. Cancel stays in the URL bar (M1-41).
- **Failed state** (padding 16, spacing 10, top aligned): a row with a 32 px circle (`YaapicDangerBackgroundBrush`)
  holding an 18 px danger icon `M12 3.5a8.5 8.5 0 1 0 0 17 8.5 8.5 0 0 0 0-17zM9 9l6 6M15 9l-6 6`
  (`YaapicDangerBrush`) and the title "No response" (`YaapicSubtitleTextStyle`); below, `ErrorMessage` in
  `YaapicBodyTextStyle` secondary, selectable. Replaces the error `InfoBar`.
- **Unresolved variables**: keep the warning `InfoBar` (styled by M1-37) with title "Unresolved variables" and the
  existing message, margin `16,8,16,0`.

## Out of scope

Cookies, Tests and Timeline tabs, Preview/Visualize modes, JSONPath filter, search, wrap, copy, save, compare,
retry button, cancelled-state variant, elapsed timer, 300 ms progress delay, large-response banner.

## Acceptance criteria

- [x] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green.
- [x] AOT publish 0 IL2xxx/IL3xxx warnings; published exe starts and exits cleanly.
- [x] Screenshots (M1-34 script with `-Send`) dark and light of: the empty state, a 201 (`create-user`), a 404
      (`missing-item`), a 503 (`server-error`), a failure (stop the stub or use `http://127.0.0.1:1`), inspected
      against `MainWindow.dc.html`, `ResponseStates.dc.html` and `States.dc.html`; differences listed in the card's
      Notes, not committed.

## Notes

- Screenshots (dark and light, AOT build, `-Send`): empty, 201, 404, 503, sending (`-NoStub`, early capture) and failed
  (`-NoStub -WaitMs 8000`) were inspected against `MainWindow.dc.html`, `ResponseStates.dc.html` and `States.dc.html`.
  The status chip (icon + text, four variants), header, Pretty/Raw control, sunken body, skeleton and empty state match.
- Deviations: the panel tabs are `YaapicPanelToggleTabStyle` toggle buttons instead of a `SelectorBar` (a
  `SelectorBarItem` only takes text, so it cannot hold the Headers count badge). The warning `InfoBar` has no title
  because the view-model message already starts with "Unresolved variables:". The response panel lost its
  `16,8,16,16` margin so it runs edge to edge like the mockup.
- Remaining visible differences: no line-number gutter or folding in the body (M1-44, code editor); the failure message
  is the OS text (localised, e.g. French on this machine), not designed copy; the 5 px editor/response separator is
  still the M1 static one (M1-46); the focus ring on the "Params" tab in `-Send` captures comes from UI Automation focus.
- The capture script stays alive after saving on this machine; the screenshots were taken with a 90 s timeout around it.

## Suggested commit message

`feat(winui): restyle the response panel and its states`
