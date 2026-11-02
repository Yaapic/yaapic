# M1-54: Avalonia request editor restyle

Status: todo
Platform: unix
Depends on: M1-52, M1-53, M2-22

## Goal

Lay out `Views/RequestEditorView.axaml` like the request section of `design/mockups/MainWindow.dc.html` and
`DSComposants1.dc.html` ("URL bar", "method picker"): header row with name and Save, one combined URL bar (method +
URL) with Send or Cancel, underlined section tabs with badges, a compact body mode bar, a sunken body editor, and a
visible draggable editor / response splitter. Equivalent of M1-41 and M1-46 (read them first). The method picker, the
all-body-kinds pane and the URL/params sync from M2 are kept: only their presentation changes.

## Context

Today: root `Grid` with 16,8 margin, a 100 px method `ComboBox` and a URL `TextBox` side by side with a plain Send
button, a stock `TabControl` (Params, Headers, Body), a row of five unstyled `RadioButton`s for the body kind with a
content-type `ComboBox`, a plain `TextBox` for the raw body, a 4 px `GridSplitter` without a look, response `2*`.

## Design decisions

- **Root**: no outer margin. Rows: header (40) | URL bar (auto, padding `16,0,16,12`) | section tabs and content
  (`*`, `MinHeight` 120) | splitter | response (`*`, `MinHeight` 180); the editor and the response start at the same
  weight (about 45 % / 55 %, DSFondations principle 02).
- **Header row** (40 px, padding `16,0,12,0`): request name as a borderless `TextBox` (`TextBox.cell` look with UI font,
  13 SemiBold), placeholder "Untitled request"; a 6 px accent dot when `IsDirty` (`AutomationProperties.Name` "Unsaved
  changes"); right: secondary "Save" button with a `Ctrl+S` text hint in `codeSmall` tertiary. Save raises a new
  `SaveRequested` routed/CLR event on `RequestEditorView`; `MainWindow` handles it from the tab content template and
  runs `Tabs.SaveActiveCommand` (use the real command name of the tabs ViewModel; same pattern as `OnCloseTab`).
- **URL bar**: one 36 px `Border` (sunken background, 1 px `YaapicBorderControlBrush`, radius `YaapicRadiusControl`)
  containing a borderless method picker (the existing `MethodBox` with `Classes="methodpicker"`: method names in
  `TextBlock.method` colours from `BrushMap.MethodBrush`, 88 px wide, a 1 px subtle divider on its right), the URL
  `TextBox` (borderless, `YaapicMonoFont`, placeholder kept), and `Button.accent` "Send" with the `Ctrl+Enter` hint
  (`IsVisible="{Binding !IsSending}"`) or a secondary "Cancel" while sending. Focus within the bar
  draws the focus ring on the whole `Border` (`:focus-within` selector). Keep `OnMethodChanged` and the existing
  index converters.
- **Section tabs**: `TabControl.panel` (M1-50): "Params", "Headers", "Body" and, when M2-23 has landed, "Auth" and
  "Settings" (the card M2-23 adds its `TabItem`s in this style). Badges: Params and Headers show `ActiveCount` as
  `TextBlock.badge` (hidden at 0); Body shows a 6 px accent dot when the body is not `none`.
- **Body pane** (`BodyKind` modes from M2-22): the five radio buttons become one `ListBox.segmented` ("none", "raw",
  "form-urlencoded", "form-data", "binary") bound to the same handler and index mapping (`OnBodyKindClick` semantics
  kept, via `SelectionChanged`); a bold "Body" `bodyStrong` label before it; the raw content-type `ComboBox` on the
  right of the bar (only when raw); the "X requests are sent without a body" text as a plain `bodySmall` tertiary
  line. The raw editor `TextBox` is borderless on `YaapicSurfaceSunkenBrush` with a 1 px
  subtle top border, `YaapicMonoFont` 12/20, padding `12,8`. The binary row uses `TextBox` + `Button` styles from M1-49.
- **Splitter**: the `GridSplitter` becomes a 5 px bar on `YaapicSurfaceSidebarBrush` with 1 px subtle borders top and
  bottom, `ResizeDirection="Rows"`, resize cursor, hover tint `YaapicOverlayHoverBrush` (style
  `GridSplitter.rows` in `Yaapic.Navigation.axaml`), keyboard focusable. Keep the row definitions as proportional
  `*` units so the drag works and neither half can collapse below its `MinHeight`.

## Out of scope

Tab context menus, authentication and settings tabs (M2-23), variable hints in the URL (M2-52), code editor
(syntax colouring, line numbers), saving the splitter position, pre-request script and tests tabs.

## Acceptance criteria

- [ ] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green; AOT publish 0 IL2xxx/IL3xxx warnings.
- [ ] Manual check on the published exe: change the method (colour follows), type a URL (params table follows), Send
      and Cancel, Ctrl+S and the Save button, Ctrl+Enter, switch every body kind, drag the splitter both ways, long
      raw body scrolls inside its pane.
- [ ] Screenshots (M1-47 script: `-Request create-user` with `--select Body`, `-Request list-users`, dark and light)
      inspected against `MainWindow.dc.html`, `DSComposants1.dc.html` and `ResponseStates.dc.html`: URL bar height and
      focus ring, Send colour, section tab underline and badges, segmented body bar, sunken editor, splitter.
      Deviations listed in the card, PNGs not committed.

## Suggested commit message

`feat(avalonia): restyle the request editor and its splitter`
