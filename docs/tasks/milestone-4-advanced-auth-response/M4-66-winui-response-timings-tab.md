# M4-66: WinUI: response Timing tab

Status: todo
Platform: windows
Depends on: M4-45, M4-61

## Goal

The response panel has a Timing tab with a waterfall of the request phases and the protocol.

## Design decisions

- Read first: `Views/ResponsePanelView.xaml(.cs)` (the toggle tabs `BodyTab`, `HeadersTab`, `TestsTab`, `OnTabClick`, panes shown by `Visibility`), M4-45.
- A fourth toggle tab "Timing" after Tests with the same style (`YaapicPanelToggleTabStyle`), and a pane:
  - a header line: `ProtocolText`, `ConnectionText`, `TotalText`;
  - for each `TimingRow` (an `ItemsControl`, `x:Bind` to `Timings`): the label (fixed 140 px column), a track `Grid` (star width) holding a `Border` bar whose `Margin.Left` is
    `OffsetFraction` of the track width and `Width` is `Fraction` of it (compute in a small converter function in `Presentation/` like the existing `...Map` classes, fed with the
    track's `ActualWidth` through `SizeChanged`; no code-behind logic beyond that), and the duration text right-aligned (fixed 72 px); `n/a` rows show a dashed muted track and no bar.
  - Bar colours: one accent-tinted neutral per phase from the design tokens (no literal colours); the Waiting phase uses the accent colour.
  - `HasTimings` false shows the empty-state text `Strings.TimingNone` ("No timing data for this response.").
- The tab is hidden while the panel shows an error or is idle, exactly like Headers.

## Out of scope

History of timings, copying values, HAR export.

## Acceptance criteria

- [ ] AOT publish of `Yaapic.App.WinUI`: 0 IL2xxx/IL3xxx warnings; the exe starts (kill it afterwards).
- [ ] Manual smoke on the published exe: a request to a local stub that delays headers by 300 ms shows a visibly longer Waiting bar; a second request to the same host shows
  "Connection reused" and no connection rows; the protocol reads `HTTP/1.1`; resizing the window keeps the bars proportional; an error response hides the data. Record anything not run.
- [ ] Strings in EN and FR; 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add the response Timing tab`
