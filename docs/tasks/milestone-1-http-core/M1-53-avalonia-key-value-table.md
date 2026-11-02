# M1-53: Avalonia key/value table restyle

Status: todo
Platform: unix
Depends on: M1-50

## Goal

Turn `Views/KeyValueEditorView.axaml` (params, headers, url-encoded body, environment variables) and
`Views/FormDataEditorView.axaml` into the bordered table of `design/mockups/DSComposants1.dc.html` ("editable
key/value table") and `ResponseStates.dc.html` (Query Params). Equivalent of M1-40 (read it first).
`KeyValueEditorViewModel.ActiveCount` already exists (M1-40 / M2-11), do not add it again.

## Design decisions

- **KeyValueEditorView**: outer `Border` 1 px `YaapicBorderDefaultBrush`, radius `YaapicRadiusMd` (6),
  `ClipToBounds`. Header row 28 px on `YaapicSurfaceSidebarBrush`, `TextBlock.caption` upper case (use `KEY` / `VALUE`
  literal strings, no converter), columns `36 | key | value | 36` with a 1 px `YaapicBorderDefaultBrush` divider left
  of the value header. Add `KeyHeader` (default "KEY") and `ValueHeader` (default "VALUE") as `StyledProperty<string>`
  on the view (code-behind, no reflection, compiled bindings) so the environments window can set `KeyHeader="VARIABLE"`.
- Columns: key `*` with `MinWidth` 140 and `MaxWidth` 280, value `2*`; use one `Grid` with `IsSharedSizeScope` for the
  header and the rows so they stay aligned.
- Rows: `ItemsControl` in a `ScrollViewer`, each row 30 px with a 1 px `YaapicBorderSubtleBrush` top border; the
  `CheckBox` centred in the 36 px column (`AutomationProperties.Name` "Enabled"); key and value cells are borderless
  `TextBox`es with a `TextBox.cell` style added to `Yaapic.Controls.axaml` (transparent background and border in every
  state, padding `10,0`, `YaapicMonoFont`, `YaapicFontSizeCode`, `MinHeight` 30, vertical centring; focused: 2 px
  `YaapicFocusRingBrush` inner border, mockup "inset 0 0 0 2px"); the value cell has a 1 px subtle left divider; remove
  `Button.icon` with `YaapicGlyphClose`, hidden for the placeholder row and shown on hover for filled rows.
- Placeholder row (`IsEmpty`): opacity 0.6; disabled rows (`Enabled` false): opacity 0.55 (mockup values), set with
  selectors on a row class bound by `Classes.placeholder="{Binding IsEmpty}"` and `Classes.disabled="{Binding !Enabled}"`.
  Tab order in a row: check box, key, value, remove. Keep every binding `TwoWay` as today.
- **FormDataEditorView**: same table with a fifth column for the kind (`Text` / `File`, a 72 px borderless `ComboBox`
  style `ComboBox.cell`) between key and value, the file picker button of the file kind as `Button.subtle`
  (keep the existing behaviour and handlers), same header, row and opacity rules.
- Remove the hand-made "Variable / Value" header of `EnvironmentsWindow.axaml` if present and use `KeyHeader`
  (the rest of that window is M1-56). Margins around the table are set by the hosts, not by the view.

## Out of scope

Description column, bulk edit, presets, drag handles, variable chips and autocomplete inside cells (M2-52 does hints).

## Acceptance criteria

- [ ] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green; AOT publish 0 IL2xxx/IL3xxx warnings.
- [ ] Manual check on the published exe: typing in the placeholder row adds a new placeholder, unchecking dims the
      row, remove works, Tab moves cell to cell, the form-data kind switch and file pick still work.
- [ ] Screenshots (M1-47 script: `-Request list-users`, the Headers tab through `--select Headers`, dark and light)
      inspected against `ResponseStates.dc.html`: header height, divider, row height, mono cells, dimmed placeholder.
      Deviations listed in the card, PNGs not committed.

## Suggested commit message

`feat(avalonia): restyle the key/value and form-data tables`
