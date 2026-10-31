# M1-41: WinUI request editor restyle

Status: done
Platform: windows
Depends on: M1-39, M1-40

## Goal

Lay out `Views/RequestEditorView.xaml` like the request section of `design/mockups/MainWindow.dc.html`: header row
with name and Save, one combined URL bar (method + URL) with Send, underlined section tabs with badges, compact body
mode bar, sunken body editor, and the editor/response split.

## Context

Today: a 20 px borderless name `TextBox`, a separate 100 px method `ComboBox`, a URL `TextBox`, an accent Send
button, a Fluent `SelectorBar`, vertical `RadioButtons` for the body kind, everything inside a 16 px padding, rows
`2*` / `*` with the response.

## Design decisions

- **Root layout**: no outer padding; rows: header (40) | URL bar (auto, padding `16,0,16,12`) | section tabs (auto) |
  section content (`*`, `MinHeight` 120) | separator (5 px) | response (`1.2*`, `MinHeight` 180). The response gets
  about 55 % of the height (DSFondations principle 02). The separator is a 5 px `Border` with
  `YaapicSurfaceSidebarBrush` and 1 px subtle borders top and bottom, not draggable in M1.
- **Header row** (40 px, padding `16,0,12,0`): request name as a borderless `TextBox` (`YaapicCellTextBoxStyle`
  from M1-40 but UI font, `YaapicBodyStrongTextStyle` metrics: 13 SemiBold), placeholder "Untitled request"; a 6 px
  `YaapicAccentFillBrush` dot when `IsDirty` (`AutomationProperties.Name` "Unsaved changes"); right: a secondary
  "Save" button with a keycap-style hint "Ctrl+S" (`YaapicCodeSmallTextStyle`, tertiary, no border). Save raises a
  new `SaveRequested` event on `RequestEditorView`; `MainWindow` handles it in the tab template
  (`SaveRequested="OnEditorSaveRequested"`, same pattern as `OnDeleteRequest`) and runs
  `Tabs.SaveActiveCommand`. The Save button is disabled when not `IsDirty`.
- **URL bar**: one `Border` of height `YaapicControlHeightSpacious` (36), `YaapicSurfaceSunkenBrush`, 1 px
  `YaapicBorderControlBrush`, radius `YaapicRadiusControl`; when any child has keyboard focus the border becomes
  `YaapicFocusRingBrush` and 2 px (code-behind on `GotFocus`/`LostFocus`, padding adjusted so content does not move).
  Inside:
  - method picker: a `DropDownButton` without border/background (mono 12 Bold, `BrushMap.Method` foreground, chevron
    tertiary, padding `12,0,10,0`) with a 1 px `YaapicBorderDefaultBrush` right divider; its `MenuFlyout` lists GET and
    POST (each item text coloured by `BrushMap.Method`, `YaapicMethodTextStyle` size 12). Keep the M1-25 rule: no boxed
    enums as `ItemsSource`/`SelectedItem`; set `ViewModel.Method` in the click handlers. `AutomationProperties.Name`
    "Method: {GET|POST}".
  - URL: borderless transparent `TextBox`, `YaapicMonoFont`, `YaapicBodyFontSize` (13), placeholder
    "Enter URL or paste text", unchanged bindings.
  - to the right of the border, 8 px gap: **Send** = `AccentButtonStyle`, height 36, padding `16,0`, content "Send"
    plus hint "Ctrl+Enter" (`YaapicCodeSmallTextStyle`, opacity 0.65, same foreground). While `IsSending`: **Cancel**
    = `YaapicDangerButtonStyle`, height 36, content: 14 px `ProgressRing` + "Cancel" + hint "Esc"; add an `Escape`
    accelerator on the view that runs `CancelCommand` only while `IsSending`.
- **Section tabs**: `SelectorBar` with `YaapicPanelTabStyle` (M1-37), padding `16,0`, 1 px subtle bottom border.
  Items: "Params" + badge `QueryParams.ActiveCount`, "Headers" + badge `Headers.ActiveCount` (badges hidden when 0,
  `YaapicCountBadgeTextStyle`, accent text on the selected tab), "Body" + a "●" badge when `BodyKind` is raw.
- **Params / Headers content**: the M1-40 table with margin `16,12,16,12`.
- **Body content**:
  - mode bar (padding `16,6`, spacing 14, font 12 secondary): two `RadioButton`s "none" / "raw" in a horizontal
    `StackPanel` (group name per editor instance), then the content type as a small `DropDownButton` (height 24, 1 px
    `YaapicBorderDefaultBrush`, radius 4, font 12, label "JSON" / "Text" / "XML" + chevron) visible for raw only,
    wired like today (index based). The GET hint "GET requests are sent without a body" moves to this bar, right
    aligned, `YaapicBodySmallTextStyle` tertiary.
  - editor: background `YaapicSurfaceSunkenBrush`, 1 px subtle top border, padding `0,6`; the existing multi-line
    `TextBox` made borderless and transparent (`YaapicMonoFont`, 12/20). M1-44 replaces it with the code editor.

## Out of scope

Breadcrumb (no collections in M1), "Generate code" and "More" buttons, Send options split part, variable chips in the
URL, Authorization/Scripts/Tests/Settings tabs, body types other than none/raw, Beautify, draggable splitter,
horizontal layout.

## Acceptance criteria

- [x] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green.
- [x] AOT publish 0 IL2xxx/IL3xxx warnings; published exe starts and exits cleanly.
- [x] Manual check on the published exe: method menu switches GET/POST and colours; Ctrl+Enter sends and Esc
      cancels `{{baseUrl}}/delay/10000` (M1-34 fixture `slow` request); Save button saves and disables; badges count
      enabled params and headers; URL bar shows the focus border.
- [x] Screenshots (M1-34 script) dark and light inspected against the request half of `MainWindow.dc.html` and
      `MainWindowLight.dc.html`; differences listed in the card's Notes.

## Notes

Implemented: `RequestEditorView.xaml` laid out as in the mockup (40 px header with name, unsaved dot and a Save button
that raises `SaveRequested`, handled by `MainWindow` through `Tabs.SaveActiveCommand`; single URL bar with the method
menu and a focus border; Send / Cancel with `Ctrl+Enter` and `Esc` hints, `Esc` accelerator active only while sending;
section tabs with count badges; body mode bar; sunken body editor; 5 px separator). The method selector is now a
`Button` with a `MenuFlyout` (GET/POST) that sets `ViewModel.Method`, which fixes the empty method `ComboBox`.
New helpers: `TextMap.Prefixed`, `VisibilityMap.WhenPositive`.

Deviations: the section tabs are `ToggleButton`s with the new `YaapicPanelToggleTabStyle` (same 2 px accent underline)
instead of a `SelectorBar`, because `SelectorBarItem` only takes text and cannot carry a badge. The Send / Cancel / Save
buttons carry an explicit `AutomationProperties.Name` so the screenshot tool finds the button, not its inner text.

Verified: build 0 warnings, 292 tests green, AOT publish without IL2xxx/IL3xxx, published exe screenshots dark and light
with `-Send` (GET and POST shown in the method selector, response renders, app survives). Not verified by a real input
run: opening the method menu and picking an item, `Ctrl+Enter` / `Esc` on the `slow` request, the Save button saving and
disabling, the URL bar focus border, and the "Clear" button of the stock `TextBox` (not seen in captures).

Remaining differences from the mockup: the `none` / `raw` radio buttons keep the stock Fluent ring (20 px, mockup 12 px);
the unsaved dot sits next to Save, not right after the name (the name box fills the row); the breadcrumb, Generate code,
More, Send options and Beautify are out of scope; the response half is restyled by M1-42; the light GET colour is the
token colour; the table does not mirror `?page=1` from the URL into Params (existing behaviour).

## Suggested commit message

`feat(winui): restyle the request editor`
