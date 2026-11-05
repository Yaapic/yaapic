# M2-61: Command palette ViewModel

Status: done
Platform: any
Depends on: M2-60

## Goal

The palette engine: sources, query, ranked results, selection and execution. No concrete commands yet (M2-62, M2-63).

## Design decisions

- In `Yaapic.ViewModels.Palette`:
  - `sealed record PaletteItem(string Id, string Title, string? Subtitle, string Category, string? Shortcut, Func<Task>
    Execute)` (`Id` is unique and stable, e.g. `command:new-request`, `request:<guid>`).
  - `interface IPaletteSource { IReadOnlyList<PaletteItem> GetItems(); }` (called each time the palette opens, so the data
    is fresh).
  - `sealed record PaletteResult(PaletteItem Item, IReadOnlyList<int> TitleMatches)`.
  - `partial class CommandPaletteViewModel : ObservableObject`, constructor `(IReadOnlyList<IPaletteSource> sources)`:
    observable `IsOpen`, `Query`, `SelectedIndex`, `string? LastError`; `ObservableCollection<PaletteResult> Results`;
    `IsEmpty` (open with a non-blank query and no result).
  - `[RelayCommand] Open()`: collects the items from every source (a source that throws is skipped and reported in
    `LastError`), clears the query, shows the recent items first, selects index 0, sets `IsOpen`. `[RelayCommand]
    Close()`.
  - Results on query change: blank query = recent items (the last 5 executed ids that still exist, newest first) then the
    others in source order; a non-blank query = `FuzzyMatcher.Match(query, Title)` (M2-60) on the title only, ordered with
    `FuzzyMatcher.Compare`, ties by recency then source order; at most 50 results; `SelectedIndex` resets to 0.
  - `[RelayCommand] MoveNext()` / `MovePrevious()`: wrap around, no-op when there are no results.
  - `[RelayCommand] Task ExecuteSelectedAsync()` and `Task ExecuteAsync(PaletteResult result)`: close the palette first,
    remember the id as most recent, then await `Execute`; an exception from `Execute` is caught into `LastError`.
- No platform types and no timers: all synchronous apart from `Execute`.

## Out of scope

Concrete sources and commands (M2-62, M2-63), views (M2-64, M2-65), persistence of recents.

## Acceptance criteria

- [x] Tests in `Yaapic.ViewModels.Tests` with fake sources: open collects items and shows source order; recents come
      first after executing; query filters and ranks (prefix beats scattered, shorter beats longer on tie); at most 50
      results; selection wraps; execute closes, runs the action and records the recent; a throwing action and a throwing
      source set `LastError` without crashing; query change resets the selection; `IsEmpty`.
- [x] 0 warnings.

## Suggested commit message

`feat(viewmodels): add command palette view model`
