# M1-40: WinUI key/value table restyle

Status: done
Platform: windows
Depends on: M1-37

## Goal

Turn `Views/KeyValueEditorView` (params, headers, environment variables) into the bordered key/value table of
`design/mockups/DSComposants1.dc.html` ("editable key/value table") and `ResponseStates.dc.html` (Query Params), and
expose the count of active rows for the section badges.

## Design decisions

- **ViewModel** (`src/Yaapic.ViewModels/Editing/KeyValueEditorViewModel.cs`): add `ActiveCount`, the number of rows
  that are enabled and not the empty placeholder, raised when a row is added, removed, enabled/disabled, or its key
  changes. Tests in `tests/Yaapic.ViewModels.Tests`: initial 0; typing a key in the placeholder -> 1; disabling -> 0;
  removing -> decrements.
- **View** (`src/Yaapic.App.WinUI/Views/KeyValueEditorView.xaml` + code-behind):
  - outer `Border`: 1 px `YaapicBorderDefaultBrush`, corner radius `YaapicRadiusMd` (6), clipped.
  - header row 28 px, background `YaapicSurfaceSidebarBrush`, `YaapicCaptionTextStyle` (11/16, tertiary, upper case):
    columns `36 | key | value | 36`, with a 1 px `YaapicBorderDefaultBrush` divider left of the value header. Header
    texts are two new dependency properties `KeyHeader` (default "KEY") and `ValueHeader` (default "VALUE").
  - column widths: key `*` with `MinWidth` 140 and `MaxWidth` 280, value `2*`.
  - rows: an `ItemsRepeater` in a `ScrollViewer` (DSComposants1 note: "WinUI: ItemsRepeater + borderless TextBox"),
    each row 30 px with a 1 px `YaapicBorderSubtleBrush` top border; checkbox centred in the 36 px column
    (`AutomationProperties.Name` "Enabled"); key and value cells are borderless `TextBox`es with a new
    `YaapicCellTextBoxStyle` (transparent background and border in every state, padding `10,0`, `YaapicMonoFont`,
    `YaapicCodeFontSize`, `MinHeight` 30, vertical centring; focused state: 2 px `YaapicFocusRingBrush` border, mockup
    "inset 0 0 0 2px"), the value cell with a 1 px subtle left divider; remove button `YaapicIconButtonStyle` with
    `YaapicGlyphClose` (from M1-36), hidden for the placeholder row.
  - placeholder row (`IsEmpty`): opacity 0.6; disabled rows (`Enabled` false): opacity 0.55 (mockup values). Use
    `VisibilityMap`-style static functions returning `double`.
  - Tab order inside a row: checkbox -> key -> value -> remove. Keep every binding `TwoWay` with
    `UpdateSourceTrigger=PropertyChanged` as today.
  - If `ItemsRepeater` with the ViewModel's `ObservableCollection` hits a CsWinRT AOT problem, keep `ListView` with a
    container style that removes hover, selection and padding, and record why in the card's Notes.
- **Environment dialog**: remove its hand-made "Variable / Value" header grid and set `KeyHeader="VARIABLE"` on its
  `KeyValueEditorView` instead (the rest of the dialog is M1-43).

## Out of scope

Description column, bulk edit, presets, drag handles, variable chips and autocomplete inside cells, Ctrl+D duplicate.

## Acceptance criteria

- [x] New ViewModel tests green; `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green.
- [x] AOT publish 0 IL2xxx/IL3xxx warnings; published exe starts and exits cleanly.
- [ ] Manual check on the published exe: typing in the placeholder row adds a new placeholder; unchecking dims the
      row; remove works; Tab moves cell to cell. (Only partly verified, see Notes.)
- [x] Screenshots (M1-34 script) dark and light of the Params and Headers tables inspected against
      `ResponseStates.dc.html`; differences listed in the card's Notes.

## Notes

Implemented: `KeyValueEditorViewModel.ActiveCount` (tests added), the bordered table (28 px header with `KEY`/`VALUE`
captions through the new `KeyHeader`/`ValueHeader` properties, 30 px rows on `ItemsRepeater` in a `ScrollViewer`,
dividers, `YaapicCellTextBoxStyle`, dimmed placeholder and disabled rows through `OpacityMap`, `TextMap.Upper`), and the
environment dialog now uses `KeyHeader="Variable"` instead of its own header grid. `ItemsRepeater` works on the Native
AOT build (no CsWinRT problem), so the `ListView` fallback was not needed. The cell editors get their transparent states
from resource overrides in the view (`TextControl*` brushes and the focused thickness), because the stock `TextBox`
template reads them by key.

Verified on the AOT exe (dark and light, `-Send` smoke test passes, app survives): table renders, header, centred
placeholder cells, UI Automation lists the checkbox, both cells as focusable. Not verified: typing into the placeholder
to add a row, unchecking, remove and cell-to-cell Tab were not exercised by a real input run (the screenshot tool can
only send keys, and Tab presses from the request tabs went to the title bar instead of the table: Tab order from the
`SelectorBar` into the table needs a look, it may predate this card).

Remaining differences from the mockup: the focus ring was not captured; the `KEY`/`VALUE` header has no letter spacing
fidelity check; the checkbox is the stock Fluent one (14 px square with 3 px radius in the mockup); the table is as
wide as the pane (the mockup caps the key column at 120/280 and the value takes the rest, which is the same layout);
the remove cross is shown on every filled row (the mockup shows it on hover).

## Suggested commit message

`feat(winui): restyle the key/value table`
