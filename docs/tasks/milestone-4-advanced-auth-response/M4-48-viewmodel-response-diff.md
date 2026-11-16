# M4-48: ViewModel: response diff

Status: todo
Platform: any
Depends on: M4-45, M4-47

## Goal

The response panel keeps the previous response and an optional pinned baseline in memory and exposes a ready-to-render diff.

## Design decisions

- Read first: ADR 0015, M4-47, `ResponsePanelViewModel.cs` (`ShowReport`, `ShowScripted`, `Clear`), how `ResponsePanelViewModel` is owned per request tab
  (a new panel per tab, so snapshots are per tab).
- `ResponseDiffViewModel` in `Yaapic.ViewModels.Responses`, owned by `ResponsePanelViewModel` (property `Diff`):
  - `Record(HttpResponseData response, DateTimeOffset at)` called by the panel after every successful response (`ShowReport`, `ShowScripted`): the
    current snapshot becomes `Previous`, the new one `Current`; a null snapshot (binary or too large) sets `UnavailableReason` (`Strings.DiffTooLarge`)
    and keeps the old snapshots unchanged for the baseline.
  - `PinCommand` (current response becomes `Baseline`; text `Strings.DiffPinnedAt(time)`), `UnpinCommand`, `CompareAgainst` choice
    (`Previous` or `Baseline`, a `bool UseBaseline`, disabled while no baseline).
  - Options: `IgnoreWhitespace`, `IgnoreVolatileHeaders` (default true); changing any option recomputes.
  - Output: `bool HasComparison` (needs two snapshots: with `UseBaseline` the baseline and the current, otherwise previous and current),
    `string SummaryText` (`Strings.DiffSummary(added, removed, headerChanges)` or `Strings.DiffNoDifferences`), `StatusLine` (`200 OK -> 404 Not Found` or
    empty), `IReadOnlyList<HeaderDiffItem> Headers` and `IReadOnlyList<DiffItem> BodyLines`.
  - `DiffItem(DiffOp Op, string Text, string OldNumber, string NewNumber, bool IsFold)`: runs of more than 6 `Equal` lines are collapsed to 3 lines
    of context on each side with one fold item in the middle (`Text` = `Strings.DiffFolded(count)`, `IsFold` true); a run at the start or end keeps
    only 3 lines next to the change. `ExpandFolds` flag to show everything.
  - Computation runs synchronously for bodies up to 1 MiB (it is bounded by the 20 000 line guard of M4-46: over the guard the VM sets
    `UnavailableReason` = `Strings.DiffTooLarge`).
  - `Clear()` on the panel clears `Previous` and `Current` but **keeps the baseline** (the user pinned it on purpose); closing the tab drops everything.
- Strings (EN and FR): `DiffTooLarge`, `DiffPinnedAt`, `DiffSummary`, `DiffNoDifferences`, `DiffFolded`, option and button labels, `ResponseTabDiff`.

## Out of scope

Views, a diff between arbitrary requests, history.

## Acceptance criteria

- [ ] Tests in `Yaapic.ViewModels.Tests`: first response has no comparison; second produces one against previous; pin then a third compares with the
  baseline; unpin; options recompute; fold behaviour on long equal runs (counts, context lines, fold item text, expanded mode); binary response leaves
  previous intact and sets the reason; `Clear` keeps the baseline; the panel records only on success (not on failures or while sending); summary
  text numbers.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(viewmodels): add the response diff view model`
