# M4-74: Avalonia: response Timing tab

Status: done
Platform: unix
Depends on: M4-45, M4-61

## Goal

The Avalonia response panel has a Timing tab with the waterfall, matching M4-66.

## Design decisions

- Read first: M4-66, `Views/ResponsePanelView.axaml(.cs)` (the `TabControl Classes="panel"` and its `TabItem` headers with badges), M4-45.
- A new `TabItem` "Timing" after Tests with the same header style. Pane: the header line (`ProtocolText`, `ConnectionText`, `TotalText`), then an `ItemsControl` of `TimingRow`: a 3-column
  `Grid` (140 px label, star track, 72 px duration text). The bar is a `Border` in a `Panel` whose horizontal placement uses a `MultiBinding`/converter in `Presentation/` (`TimingBarMap`, a
  pure function of `OffsetFraction`, `Fraction` and the track width, using `Bounds.Width` of the track through `Binding` to `Bounds`); no logic in code-behind.
- Colours from the brush resources (accent for Waiting, neutral tints for the others); `n/a` rows with a dashed muted track; the empty state text `Strings.TimingNone`.

## Out of scope

HAR export, history.

## Acceptance criteria

- [x] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings; the exe starts and exits cleanly (kill it afterwards).
- [ ] Runtime smoke on macOS or Linux as in M4-66; if not possible, say so in the Notes and tick only the build and AOT items.
- [x] A unit test for `TimingBarMap` in the Avalonia-independent place the other `*Map` helpers are tested (check `tests/` for where `ResponseMap` is tested; if none, the map is
  covered by view model tests only and the card says so).
- [x] Strings in EN and FR; 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add the response Timing tab`

## Notes

- The bar is placed with three star columns fed by `TimingBarMap` multi-value converters (as in WinUI) instead of measuring `Bounds.Width`: it stays proportional on resize with no code-behind.
- No test project covers the Avalonia `*Map` helpers (`ResponseMap` has none), so `TimingBarMap` is covered by the view model tests of `TimingRow` only.
- Runtime smoke on macOS/Linux was not possible (Windows host); only the build and the AOT publish (0 IL warnings, exe starts) were verified.
