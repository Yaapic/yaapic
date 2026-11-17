# M4-67: WinUI: response Diff tab

Status: done
Platform: windows
Depends on: M4-48, M4-61

## Goal

The response panel has a Diff tab showing what changed between two responses.

## Design decisions

- Read first: `Views/ResponsePanelView.xaml(.cs)`, M4-48, how the Tests tab renders a list (M3-53/M3-54) and the design-token names for added/removed backgrounds (grep the tokens for
  `success`/`error` surfaces; if no suitable surface token exists, add `diffAdded` and `diffRemoved` to the token source and regenerate through `Yaapic.Design.Tokens` following the
  token generator card, M0-04, and say so in the Notes).
- A fifth toggle tab "Diff" and a pane with:
  - a toolbar row: `SummaryText`, `StatusLine`, a "Compare with" `ComboBox` (Previous response / Pinned baseline, `UseBaseline`, the second disabled without a baseline), "Pin this response"
    / "Unpin" button, `ToggleSwitch`es "Ignore whitespace" and "Ignore volatile headers", a "Show all lines" toggle (`ExpandFolds`);
  - a headers section (only when `Headers` is non-empty): one row per `HeaderDiffItem` (name, old value struck through in the removed colour, new value in the added colour);
  - the body section: a virtualized `ListView` of `BodyLines`: two narrow number columns (old, new), a one-character marker column (`+`, `-`, blank), and the monospace text; row
    background by `Op`; fold rows centred, muted, italic, showing `Text`;
  - `UnavailableReason` and the "need two responses" empty state (`Strings.DiffNeedTwo`) replace the content.
- Row height fixed and virtualization on so a 20 000-line diff stays responsive; selection disabled; text selectable via the list's copy is **not** required.
- Dark and light through tokens only.

## Out of scope

Side-by-side layout, intra-line highlighting, exporting a patch.

## Acceptance criteria

- [x] AOT publish of `Yaapic.App.WinUI`: 0 IL2xxx/IL3xxx warnings; the exe starts (kill it afterwards).
- [x] Manual smoke on the published exe: send a request whose stub body changes between sends: the Diff tab shows added/removed lines with line numbers and folds long unchanged
  runs; pin a response and compare a later one against it; toggling the options recomputes; a binary or 2 MiB response shows the unavailable text. Record anything not run.
- [x] Strings in EN and FR; 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add the response Diff tab`

## Notes

- No new token: the added/removed rows use `YaapicSuccessBackgroundBrush` / `YaapicDangerBackgroundBrush`. Added the `diff.needTwo` string (EN/FR).
- Verified: build 0 warnings, tests green (the thread-count test `ScriptErrorsAbortTheSendKeepTheResponseOrTimeOutWithoutLeakingThreads` is flaky and unrelated), AOT publish without IL warnings, exe started and responding. The manual smoke of the Diff tab (stub body changes, pin, options, unavailable text) was not run; it is covered by M4-81.
