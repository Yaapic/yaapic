# M1-16: MVVM toolkit and key/value editor ViewModel

Status: todo
Platform: any
Depends on: M1-11

## Goal

Adopt CommunityToolkit.Mvvm in `Yaapic.ViewModels` and build the reusable key/value table ViewModel (params, headers,
variables). Creates `tests/Yaapic.ViewModels.Tests`.

## Design decisions

- Package `CommunityToolkit.Mvvm`, latest stable 8.x on nuget.org, pinned in `Directory.Packages.props`, referenced by
  `Yaapic.ViewModels` only, justified in `docs/dependencies.md` (MIT, source generators, no runtime reflection on the
  paths we use). Allowed: `ObservableObject`, `[ObservableProperty]` on **fields** (`private string _key = "";`),
  `[RelayCommand]`, `AsyncRelayCommand`. Forbidden (reflection): `ObservableValidator`, `Messenger.RegisterAll`.
- `tests/Yaapic.ViewModels.Tests` mirrors `Yaapic.Architecture.Tests.csproj`, references `Yaapic.ViewModels`,
  `Yaapic.Core`, `Yaapic.Domain`, `Yaapic.UI.Abstractions`, `Yaapic.TestDoubles`; added to `Yaapic.sln`.
- Namespace `Yaapic.ViewModels.Editing`:
  - `partial class KeyValueRowViewModel : ObservableObject` with observable `Key` (string, ""), `Value` (string, ""),
    `Enabled` (bool, true), `bool IsEmpty => Key.Length == 0 && Value.Length == 0`.
  - `partial class KeyValueEditorViewModel : ObservableObject`:
    - `ObservableCollection<KeyValueRowViewModel> Rows`, always ending with exactly one empty "placeholder" row.
    - When the placeholder row becomes non-empty, a new placeholder row is appended.
    - `[RelayCommand] void RemoveRow(KeyValueRowViewModel row)`: removes it, except the placeholder (no-op).
    - `void Load(IEnumerable<KeyValueEntry> entries)`: replaces all rows with the entries plus a placeholder; does
      **not** raise `Changed`.
    - `IReadOnlyList<KeyValueEntry> ToEntries()`: rows in order, skipping empty rows.
    - `event EventHandler? Changed`: raised when a row's `Key`, `Value` or `Enabled` changes, or a row is removed.
- Row property-changed subscriptions are removed when a row is removed (no leaks).

## Out of scope

Bulk edit, drag reordering, descriptions, auto-completion of header names.

## Acceptance criteria

- [ ] Tests: new editor has one placeholder; typing a key adds a placeholder; `RemoveRow` on placeholder is a no-op;
      `Load` + `ToEntries` round trip keeps order and `Enabled`; empty rows skipped; `Changed` raised on edit, toggle and
      remove, and not on `Load`.
- [ ] 0 warnings (AOT analyzers on), architecture tests green.

## Suggested commit message

`feat(viewmodels): add MVVM toolkit and key/value editor`
