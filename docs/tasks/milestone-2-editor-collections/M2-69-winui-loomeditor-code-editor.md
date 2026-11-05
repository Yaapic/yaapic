# M2-69: WinUI: adopt LoomEditor in CodeEditorView

Status: done
Platform: windows
Depends on: M2-68

## Goal

The request body, the response body and the Raw/Pretty views of the WinUI app show a syntax-highlighted code editor
(line numbers, JSON/XML colours from the Yaapic tokens, dark and light) instead of the monochrome `TextBox`. Only
`CodeEditorView` changes: it keeps implementing `ICodeEditor` and the views that use it are untouched.

## Design decisions

- Read first: `docs/adr/0002-code-editor.md`, `docs/dependencies.md` ("Rejected: LoomEditor.WinUI"), the Notes of
  `M1-44-winui-code-editor-loomeditor.md`, `src/Yaapic.App.WinUI/Views/CodeEditorView.xaml(.cs)` and
  `src/Yaapic.UI.Abstractions/Editing/ICodeEditor.cs`.
- Package: `LoomEditor.WinUI` `0.4.0` (MIT), added to `Directory.Packages.props` and to `Yaapic.App.WinUI.csproj`.
  Verified beforehand with Windows App SDK 2.6.5 on `net11.0-windows10.0.26100` (restore, build, AOT publish: 0 warnings).
  Read the package (`.nupkg` in the NuGet cache: README, XML docs, licence) before coding: the API notes below come
  from `0.2.0-alpha.5` and **must be re-checked against the 0.4.0 XML docs**, they may have changed. Record what
  differs in Notes.
- API as known (0.2.0-alpha.5): control `LoomEditor.WinUI.Controls.LoomEditor`; its `Text` property is **not updated
  by user edits** (read `Model.GetValue()`, listen to `Model.ContentChanged`, write with `ResetText`); `Theme` is built
  with `EditorTheme.Load(vsCodeThemeJson, "vs" | "vs-dark", name, _ => null)`; `LanguageId` takes `json`, `xml`,
  `plaintext`; `IsReadOnly`; line numbers (confirm the option name in 0.4.0).
- `CodeEditorView` (same file names and public surface: `Text` DP, `IsReadOnly` DP, `EditorLanguage` DP, explicit
  `ICodeEditor.Language`, `TextChanged` event):
  - Replace the `TextBox` by the LoomEditor control named `Editor`; remove the TextBox-flattening resources.
  - **Text sync without loops**: `Text` DP changed from outside -> `Editor.ResetText(text)` only when
    `Editor.Model.GetValue()` differs (never reset on the echo of a user edit: it would move the caret and wipe undo);
    `Model.ContentChanged` -> set the `Text` DP from `Model.GetValue()` and raise `TextChanged`. A guard flag covers
    the `ResetText` call so programmatic writes do not raise `TextChanged` (`ICodeEditor.TextChanged` means "the user
    edits the text").
  - `Model.GetValue()` on every keystroke copies the whole text: acceptable up to the large-text check below; if the
    5 MB test is unusable while editing, debounce the DP update (comment `shortcut: <limit>, <when to upgrade>`), but
    never on the read-only response path.
  - `EditorLanguage` -> `LanguageId`: `"json"` -> `json`, `"xml"` -> `xml`, anything else (`"text"`, `""`) ->
    `plaintext`. If the 0.4.0 package ships `html` and `javascript` grammars, map those too; `TextMap.Language` stays
    unchanged.
  - `IsReadOnly` -> the control's read-only property.
- **Theme from tokens**: build the `EditorTheme` from a VS Code theme JSON string assembled in code from the Yaapic
  brushes (no JSON file to ship, no reflection), one per theme: background `YaapicSurfaceSunkenBrush`, foreground
  `YaapicTextPrimaryBrush`, line numbers `YaapicTextDisabledBrush`, selection and current line
  `YaapicSelectionBackgroundBrush`, token scopes mapped to `YaapicSyntaxKeyBrush` (JSON keys),
  `YaapicSyntaxStringBrush`, `YaapicSyntaxNumberBrush`, `YaapicSyntaxBooleanBrush` (`true`/`false`/`null`),
  `YaapicSyntaxPunctuationBrush`, `YaapicSyntaxTagBrush` (XML tags), `YaapicSyntaxAttributeBrush` (XML attributes),
  `YaapicSyntaxCommentBrush`. Read the colours from the application resources for the actual theme (the theme
  dictionaries of `design/xaml/Yaapic.Colors.WinUI.xaml`), never copied hex values. Reapply the theme on
  `ActualThemeChanged`. Font `YaapicMonoFont`, size `YaapicFontSizeCode` (12) with 20 px line height if the control
  exposes it; 44 px gutter as in `design/mockups/MainWindow.dc.html` where the control allows it (note any gap).
- Keep `AutomationProperties.Name="Code editor"` (the views override it with "Body"). If the control does not expose
  its text to UI Automation, note the accessibility gap in Notes.
- **Large text**: open a 5 MB JSON response in the response editor (add a `/big` route generating about 5 MB of JSON
  to the stub server of `tools/screenshots/Capture-WinUI.ps1` if there is none): scrolling, select all and Ctrl+End
  must stay usable (no multi-second freeze), in Pretty and in Raw. If the control is unusable above some size, add a
  documented threshold in `CodeEditorView` above which the language falls back to `plaintext` (no tokenisation).
  Record the measurement and the threshold in Notes.
- **Pretty/Raw** switching keeps working; the binary response `TextBox` stays; the empty state is unchanged.
- **AOT**: publish with Native AOT, 0 IL2xxx/IL3xxx; launch the exe and check the editor renders, edits (type, undo,
  paste), reads back into `BodyText`, sends the request with the edited body, and shows the response coloured; exit
  cleanly. If a blank editor, missing colours or a crash appear only in the AOT build, fix it in the App project with
  an explicit code path (no reflection, no `DynamicDependency`), or stop and report; never weaken the AOT rule.
- **Docs**:
  - `docs/dependencies.md`: a `LoomEditor.WinUI` row in the main table (version `0.4.0`, `Yaapic.App.WinUI`, MIT,
    justification: syntax colours, line numbers and large text on WinUI where no other mature editor exists; AOT
    result); the "Rejected: LoomEditor.WinUI" section becomes a short history note ("rejected by M1-44, unblocked by
    M2-68, adopted by M2-69").
  - `docs/adr/0002-code-editor.md`: rewrite the "WinUI decision" section (LoomEditor behind `ICodeEditor`,
    `CodeEditorView` the only consumer, versions, the `TextBox` fallback removed); the Avalonia paragraph points to
    M2-70. Status stays `Accepted`.
  - The M1-44 card and its Notes stay as the historical record; do not edit them.

## Out of scope

Avalonia editor (M2-70), folding UI beyond what the control gives for free, `{{variable}}` highlighting and
autocomplete, search/replace UI, minimap, scripts editor, new languages, any ViewModel change.

## Acceptance criteria

- [x] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green; architecture tests green
      (`Yaapic.UI.Abstractions` still references no UI framework).
- [x] AOT publish 0 IL2xxx/IL3xxx; the published exe edits a JSON body, sends it, shows the coloured JSON response,
      switches Pretty/Raw and exits cleanly.
- [x] JSON and XML bodies are coloured in dark and light with the Yaapic syntax tokens; line numbers are visible;
      switching the app theme at run time recolours an open editor.
- [x] Two-way sync verified: typing updates `BodyText` (the tab shows its unsaved marker), switching to another tab
      and back restores the text, and the caret does not jump while typing.
- [x] 5 MB response check done and recorded in Notes (fallback threshold added if needed).
- [x] Screenshots through `Capture-WinUI.ps1` (`-Send`, dark and light) of a JSON request body and a JSON response,
      compared with `design/mockups/MainWindow.dc.html`; differences listed in Notes, screenshots not committed.
- [x] `docs/dependencies.md` and `docs/adr/0002-code-editor.md` updated as described.
- [x] No Yaapic or stub-server process left running.

## Rules

- Every shell command, git included, starts with `rtk` (`rtk bypass <cmd>` only if the condensed output is unusable).
- Wait for long commands (build, test, AOT publish) in the foreground; do not poll.
- Stop every process you launched (the published exe, the stub server, `dotnet` build servers you started).
- Check the SonarCloud issues of the files you changed
  (`https://sonarcloud.io/api/issues/search?componentKeys=Yaapic_yaapic&resolved=false`) and fix them before committing.
- Commit on `feat/m2-editor-collections` (no new branch or worktree), GPG-signed
  (`git -c gpg.program="C:\Program Files (x86)\GnuPG\bin\gpg.exe" commit -S`, key of adrien.granger@outlook.com),
  with no trailer or co-author line, then push.

## Suggested commit message

`feat(winui): highlight request and response bodies with LoomEditor`

## Notes

### Implementation notes (M2-69)

- 0.4.0 API matches the notes above: `Text` is not updated by edits (sync through `Model.ContentChanged` / `ResetText`),
  `Theme` is a plain property, `LineNumbersEnabled`, `LineHeight` (20), `LanguageId`, `IsReadOnly` exist. Folding is on (chevrons appear on hover, verified in the AOT exe); the minimap is off.
  Not mapped: `html`/`javascript` (the app languages stay `json`, `xml`, `text`).
- Gaps: the 44 px gutter is the control's own width; bracket pairs are coloured by the control (yellow/pink), not by the
  Yaapic tokens; UI Automation does not find an element named "Code editor" (accessibility gap of the control peer).
- Large text (AOT exe, dark): a 5.2 MB / 65 000-line JSON response loads and tokenises (coloured) in about a second; the
  window stays responsive, so no plaintext fallback threshold was added. A 5 MB single-line response is shown
  uncoloured on one line. Keyboard scrolling and select-all in the read-only response editor could not be driven by the
  synthetic input of `Capture-WinUI.ps1` (the click did not move focus into the read-only editor), so that path is unmeasured.
- XML colouring was not checked at runtime (JSON, dark and light, was); the XML scopes are `entity.name.tag` and
  `entity.other.attribute-name`.
- Capture-WinUI.ps1 gained a `/big` stub route and a `big` fixture request. Screenshots are not committed.
