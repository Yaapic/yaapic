# M4-45: ViewModel: response timings and protocol

Status: done
Platform: any
Depends on: M4-36, M4-39

## Goal

`ResponsePanelViewModel` exposes the timings as display rows (for a waterfall bar) and the protocol.

## Design decisions

- Read first: `src/Yaapic.ViewModels/Responses/ResponsePanelViewModel.cs` (`ShowReport`, `ShowScripted`, `Clear`, `DurationText`),
  `docs/spikes/m4-timings.md`, M4-35.
- `public sealed record TimingRow(string Label, string DurationText, double Fraction, double OffsetFraction, bool IsMeasured)` in
  `Yaapic.ViewModels.Responses`: `Fraction` is the share of the **total** (`Duration`) for the bar width, `OffsetFraction` the sum of the previous
  rows' fractions (bar start), both clamped to 0..1; `DurationText` is `"12 ms"` / `"1.34 s"` (reuse the existing duration formatter of
  `DurationText`), or `"n/a"` (`Strings.TimingNotAvailable`) when `IsMeasured` is false.
- `ResponsePanelViewModel` gains `IReadOnlyList<TimingRow> Timings`, `bool HasTimings`, `string ProtocolText` (empty when unknown), `string
  ConnectionText` (`Strings.TimingConnectionReused` / `Strings.TimingNewConnection`), `string TotalText`.
  Rows in order: DNS lookup, TCP connect, TLS handshake, Waiting (TTFB), Download; unmeasured connection phases are shown as `n/a` rows only when the
  connection is **new**; a reused connection shows `Strings.TimingConnectionReused` and no connection rows.
- Filled by `ShowReport`, `ShowScripted` (from `response.Timings`), emptied by `Clear`, `ShowSending` and failures (`HasTimings` false).
- Rounding: fractions of a row whose duration is 0 are 0; a row never has a fraction below 0.005 when its duration is positive (so it stays visible).
- Strings (EN and FR): the five labels, `TimingNotAvailable`, `TimingConnectionReused`, `TimingNewConnection`, `ResponseTabTiming`.

## Out of scope

Views, history storage, charts.

## Acceptance criteria

- [x] Tests in `Yaapic.ViewModels.Tests`: full new-connection timings give five rows with fractions summing to <= 1 and increasing offsets; a reused
  connection gives two rows plus the reused text; null timings give `HasTimings` false; null phases give `n/a`; a failure and `Clear` empty the
  rows; the protocol text; very small and zero durations; duration formatting units.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(viewmodels): expose response timings and the protocol`
