# M1-55: Avalonia response panel restyle

Status: done
Platform: unix
Depends on: M1-54

## Goal

Make `Views/ResponsePanelView.axaml` match the response section of `design/mockups/MainWindow.dc.html` and the states
of `ResponseStates.dc.html` and `States.dc.html`: one header line with panel tabs and status, a Pretty/Raw segmented
control, a sunken code surface, and designed empty, sending and failed states. Equivalent of M1-42 (read it first for
the icon geometries and texts).

## Design decisions

- Panel background `YaapicSurfaceBaseBrush`. Keep every binding and every `ResponseMap` state converter; only the
  presentation changes.
- **Header** (36 px, padding `16,0,12,0`, 1 px subtle bottom border), visible in every state except Empty. Left:
  `TabControl.panel` headers "Body" and "Headers" with the `Headers.Count` badge (`TextBlock.badge`, hidden at 0);
  because the tab headers live in the strip of a `TabControl`, put the status block in the same row by giving the
  `TabControl` template a right-hand `ContentPresenter` area, or build the header as a `Grid` with a `ListBox` styled
  as panel tabs bound to a `SelectedIndex` that drives a `Panel` of two views; choose the simpler and keep keyboard
  navigation. Right (spacing 10, mono 12): the status chip (`Border.chip`, background and foreground from
  `ResponseMap.StatusBackground` / `StatusForeground`) with a 12 px icon and `StatusText` (SemiBold), then
  `DurationText` and `SizeText` (secondary). The four stroked icons (3 px, chip foreground; never colour alone) are the
  geometries of M1-42, stored as `YaapicGlyphStatusSuccess`, `...ClientError`, `...ServerError`, `...Info` in
  `Yaapic.Controls.axaml`, shown by four `PathIcon`s whose `IsVisible` uses new `ResponseMap` converters per category
  (add them to `Presentation/ResponseMap.cs` with XML docs). The chip has `AutomationProperties.Name` = `StatusText`
  and `AutomationProperties.LiveSetting="Polite"`.
- **Body tab**: toolbar 40 px (padding `16,0,12,0`) with Pretty / Raw as `ListBox.segmented` (M1-50), keeping the
  `OnPretty` / `OnRaw` semantics (use `SelectionChanged` and the `ViewMode` converters). Body surface
  `YaapicSurfaceSunkenBrush`, 1 px subtle top border, the read-only body `TextBox` (and the binary one) borderless and
  transparent, `YaapicMonoFont` 12/20, padding `12,2`.
- **Headers tab**: rows of line height 24 with a 1 px subtle bottom border and padding `14,0`; key column 170 px in
  `YaapicSyntaxKeyBrush`, value `YaapicTextPrimaryBrush`, mono 12, selectable, value ellipsis with the full value in a
  tooltip (as today, restyled).
- **Empty state** (sunken, centred, spacing 12): the 64 x 40 illustration path of M1-42 (3 px round caps,
  `YaapicBorderOverlayBrush`), "The response will appear here." (secondary), and a hint line with two `Border.keycap`
  "Ctrl" + "Enter" followed by "to send" (12, tertiary).
- **Sending state**: header shows "Sending" (`bodyStrong`) instead of tabs and chip; under it the indeterminate 2 px
  `ProgressBar` (M1-50); then three `Border.skeleton` bars at 55 %, 80 % and 35 % of the width (star columns), padding
  16. Cancel stays in the URL bar (M1-54).
- **Failed state** (padding 16, spacing 10, top aligned): a 32 px circle (`YaapicDangerBackgroundBrush`) holding an
  18 px danger icon (geometry of M1-42, `YaapicDangerBrush`) next to the title "No response" (`subtitle`); below,
  `ErrorMessage` in `body` secondary, selectable. Replaces the current danger-tinted title box.
- **Unresolved variables**: `Border.infobar.warning` with the existing message, margin `16,8,16,0`.
- M2-24 (copy and save buttons) adds its buttons to this header: leave room for them at the right of the status block
  and do not add them here.

## Out of scope

Cookies, Tests, Timeline tabs, Preview and Visualize modes, JSONPath filter, search, wrap, copy, save (M2-24), compare,
retry, cancelled variant, elapsed timer, large-response banner, code editor (syntax colouring, line numbers).

## Acceptance criteria

- [x] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green; AOT publish 0 IL2xxx/IL3xxx warnings.
- [x] Manual check on the published exe: Pretty/Raw switch, Headers tab with a long value tooltip, binary response
      text, unresolved-variable warning, Cancel during a slow request, failed request message selectable.
- [x] Screenshots (M1-47 script with `-Send`, dark and light) of the empty state, a 201 (`create-user`), a 404
      (`missing-item`), a 503 (`server-error`), a failure (`-NoStub`) and sending (`slow`, `-WaitMs 800`) inspected
      against `MainWindow.dc.html`, `ResponseStates.dc.html` and `States.dc.html`: chip colours and icons, segmented
      control, sunken body, skeleton, empty and failed layouts. Deviations listed in the card, PNGs not committed.

## Notes

- Screenshots (dark and light, AOT build): empty, 201, 404, 503 and failed inspected against the mockups; chip colours
  and icons, segmented control, sunken body, empty and failed layouts match. The status block is overlaid on the right of
  the `TabControl` strip (a `Panel` around the tabs), so M2-24 can add its buttons next to it.
- Deviations: the sending state could not be captured (the script waits for the response; with `-NoStub` the connection
  is refused at once), it was checked in the XAML only. The failure message is the localised OS text. The manual check
  on the published exe was not interactive; Pretty/Raw, tooltips and Cancel keep their existing bindings.

## Suggested commit message

`feat(avalonia): restyle the response panel and its states`
