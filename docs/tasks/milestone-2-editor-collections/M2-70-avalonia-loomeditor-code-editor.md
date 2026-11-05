# M2-70: Avalonia: adopt LoomEditor in CodeEditorView

Status: todo
Platform: unix
Depends on: M2-69

## Goal

The Avalonia app shows a syntax-highlighted code editor (line numbers, folding, JSON/XML colours from the Yaapic
tokens, dark and light) for the raw request body and the response body instead of monochrome `TextBox`es, behind the
same `ICodeEditor` abstraction as WinUI (ADR 0002). It is the Avalonia twin of M2-69: same control family
(`LoomEditor`), same behaviour, same theme rules. The card replaces the former AvaloniaEdit + TextMate plan, which is
abandoned (no TextMate registry, no bundled grammars: LoomEditor ships its own).

## Design decisions

- Read first: `M2-69-winui-loomeditor-code-editor.md` (design and Notes), `src/Yaapic.App.WinUI/Views/CodeEditorView.xaml(.cs)`
  (the behaviour to mirror, almost line for line), `docs/adr/0002-code-editor.md`, `docs/dependencies.md`,
  `src/Yaapic.UI.Abstractions/Editing/ICodeEditor.cs`, `src/Yaapic.App.Avalonia/Views/RequestEditorView.axaml` (raw body
  `TextBox.editor`), `Views/ResponsePanelView.axaml` (read-only body `TextBox`), `Styles/Yaapic.Controls.axaml`
  (`TextBox.editor`), `Presentation/BrushMap.cs`.
- **Package**: `LoomEditor.Avalonia` `0.5.0` (MIT, NuGet), added to `Directory.Packages.props` and
  `Yaapic.App.Avalonia.csproj`. It brings `LoomEditor.Rendering/View/Core/Themes/TextMate` `0.5.0` transitively. Checked
  beforehand in a scratch project (net11.0, win-x64, Native AOT): **it depends on `Avalonia >= 12.1.5`, the repo is on
  `12.1.4`**, so restore fails with `NU1605` until Avalonia, Avalonia.Desktop and Avalonia.Themes.Fluent are bumped to
  `12.1.5` in `Directory.Packages.props` (do that in this card; no other Avalonia change). With 12.1.5 the scratch
  project restores, builds and AOT-publishes with 0 IL2xxx/IL3xxx (only the two existing `Avalonia.Win32` ILC
  `UnmanagedCallersOnly` messages); the publish carries `onig.dll` (native TextMate regex engine) next to the exe, so
  check it is present in the app publish folder and, on macOS/Linux, that the native library for the RID is shipped.
  Note: `LoomEditor.Core 0.5.0` appeared in the NuGet index a few minutes after the other packages; if restore says
  `NU1102 ... Nearest version: 0.4.0`, retry with `dotnet restore --no-cache`.
- **API versus LoomEditor.WinUI 0.4.0** (diffed from the XML docs): the control is
  `LoomEditor.Avalonia.Controls.LoomEditor` (xmlns `using:LoomEditor.Avalonia.Controls`). Same members as WinUI that
  matter here: `Text` (not updated by user edits), `ResetText(string)`, `Model.GetValue()`, `Model.ContentChanged`
  (namespace `LoomEditor.Core.Model`), `Theme` (`LoomEditor.Themes.EditorTheme.Load(json, "vs" | "vs-dark", name, _ => null)`),
  `LanguageId`, `IsReadOnly`, `LineNumbersEnabled`, `FoldingEnabled`, `MinimapEnabled`, `LineHeight`. Differences:
  **App.axaml must add `<loom:LoomEditorStyles />`** (`xmlns:loom="using:LoomEditor.Avalonia"`) after the Fluent theme,
  otherwise the control has no template; Avalonia has no `OverviewRulerEnabled` and no `Automation*` properties
  (the control ships its own `LoomEditorAutomationPeer`, check with a screen reader or UIA tool and record it); events
  take Avalonia types. Re-check the XML docs in the NuGet cache
  (`loomeditor.avalonia/0.5.0/lib/net11.0/LoomEditor.Avalonia.xml`) before coding and record any further difference in Notes.
- `src/Yaapic.App.Avalonia/Views/CodeEditorView.axaml(.cs)` (new `UserControl` implementing `ICodeEditor`; public
  surface as in WinUI: `Text` styled property (two-way by default), `IsReadOnly`, `EditorLanguage`, explicit
  `ICodeEditor.Language`, `TextChanged` event): contains the LoomEditor named `Editor` with `LineNumbersEnabled`,
  `FoldingEnabled` **true** (the WinUI card had to re-enable it in 0c84084: keep it on), `MinimapEnabled` false,
  `FontFamily` `YaapicMonoFont`, `FontSize` `YaapicFontSizeCode`, `LineHeight` 20.
  - **Text sync without loops**, as in WinUI: `Text` changed from outside -> `Editor.ResetText(text)` only when
    `Editor.Model.GetValue()` differs (never on the echo of a user edit: it would wipe caret and undo); `Model.ContentChanged`
    -> set `Text` from `Model.GetValue()` and raise `TextChanged`; a guard flag around `ResetText` so programmatic writes
    do not raise `TextChanged`. Mind the Avalonia threading rule (events on the UI thread, which `ContentChanged` is).
  - `EditorLanguage` -> `LanguageId`: `"json"` -> `json`, `"xml"` -> `xml`, anything else -> `plaintext`.
  - `IsReadOnly` -> `Editor.IsReadOnly`.
  - Avalonia owns no `Language` conflict (unlike WinUI's `UIElement.Language`), but keep the name `EditorLanguage`
    and the explicit `ICodeEditor.Language` for parity.
- **Theme from tokens**: same VS Code theme JSON built in code as `ApplyTheme()` of the WinUI view (copy it, do not
  invent a second mapping): background `YaapicSurfaceSunkenBrush`, foreground `YaapicTextPrimaryBrush`, line numbers
  `YaapicTextDisabledBrush` (active `YaapicTextSecondaryBrush`), selection and current line
  `YaapicSelectionBackgroundBrush`, scopes mapped to the `YaapicSyntax*Brush` tokens. Colours are read from the
  application resources for the current theme variant (`TryFindResource(key, ActualThemeVariant, out var v)`; see how
  `Presentation/BrushMap.cs` and the other views resolve themed brushes; add a helper there if none exists), never
  copied hex values. Apply on `AttachedToVisualTree` and on `ActualThemeVariantChanged`; pick `vs` for light and
  `vs-dark` for dark.
- **Usage**: replace the raw-body `TextBox.editor` in `RequestEditorView.axaml` (`Text` two-way with `BodyText`,
  `EditorLanguage` from the content type through a new converter in `Presentation/EditorMap.cs` that mirrors
  `TextMap.Language` of WinUI) and the text `TextBox` of `ResponsePanelView.axaml` (read-only, `Text` = `BodyText`,
  `EditorLanguage` = `BodyLanguage`, visible when `!IsBinary`). Set `AutomationProperties.Name` ("Body") as the WinUI
  views do. The binary view `TextBox` and the empty state stay. Remove the `TextBox.editor` style from
  `Yaapic.Controls.axaml` if nothing else uses it. Pretty/Raw switching keeps working. Compiled bindings only.
- **Large text**: `tools/screenshots/workspace/requests/big.request.yaml` and a `/big` route in `Capture-WinUI.ps1`
  exist; `Capture-Avalonia.ps1` has no `/big` route and no `big` entry in `$ids` yet (id
  `11111111-0000-0000-0000-000000000008`): add both (same generator as WinUI, about 5 MB). Open the response in
  Pretty and Raw: scrolling, select all and Ctrl+End must stay usable (no multi-second freeze). If the control is unusable
  above some size, add a documented threshold in `CodeEditorView` above which the language falls back to `plaintext`.
  Record the measurement and the threshold in Notes.
- **AOT**: publish with Native AOT (`rtk dotnet publish src/Yaapic.App.Avalonia -c Release -r win-x64`, or the host
  RID), 0 IL2xxx/IL3xxx beyond the existing `Avalonia.Win32` messages (no `NoWarn`, no `#pragma`, no
  `DynamicDependency`). Launch the exe and check at runtime (not only through the screenshot flag): colours in the
  request body and the response viewer, typing, undo (Ctrl+Z), folding (gutter chevron), both themes, tab switch and
  back, exit cleanly. If something works in the JIT build but not in AOT, fix it with an explicit code path or stop and
  report.
- **Docs**:
  - `docs/dependencies.md`: a `LoomEditor.Avalonia` row (version `0.5.0`, `Yaapic.App.Avalonia`, MIT, justification:
    same editor family as WinUI, syntax colours, line numbers, folding and large text on Avalonia; AOT result, the
    `Avalonia 12.1.5` floor and `onig.dll`); update the Avalonia version rows to `12.1.5`; extend the "History" note
    with the LoomEditor.Avalonia adoption. No AvaloniaEdit or TextMate rows (nothing bundled).
  - `docs/adr/0002-code-editor.md`: replace the AvaloniaEdit wording in "Options", "AOT risks" and the last sentence by
    an "Avalonia decision (M2-70)" section (LoomEditor.Avalonia behind `ICodeEditor`, `CodeEditorView` the only consumer,
    versions, AvaloniaEdit abandoned and why: one editor family for both stacks). Status stays `Accepted`.
  - `docs/tasks/README.md`: the M2-70 row (title, link, depends) and the sentence "AvaloniaEdit on Avalonia" updated;
    set the row to `done` when finished. `M2-74`/`M2-75` mention AvaloniaEdit: replace by LoomEditor in one line each.
    M1 cards and `docs/screenshots/m1-avalonia/README.md` stay as historical records.

## Out of scope

WinUI, `{{variable}}` highlighting and autocomplete, search/replace UI, minimap, scripts editor, new languages, any
ViewModel change, LoomEditor features beyond highlighting, line numbers and folding.

## Acceptance criteria

- [ ] `dotnet build Yaapic.CrossPlatform.slnf -c Release` 0 warnings; `dotnet test --solution Yaapic.CrossPlatform.slnf -c Release`
      green; architecture tests green (`Yaapic.UI.Abstractions` still free of UI frameworks).
- [ ] AOT publish of `Yaapic.App.Avalonia` 0 new IL2xxx/IL3xxx; the published exe edits a JSON body, sends it, shows the
      coloured JSON response, switches Pretty/Raw, folds a block, undoes an edit and exits cleanly.
- [ ] JSON and XML bodies are coloured in dark and light with the Yaapic syntax tokens; line numbers visible; switching
      the theme at run time recolours an open editor.
- [ ] Two-way sync verified: typing updates `BodyText` (the tab shows its unsaved marker), tab switch and back restores
      the text, the caret does not jump while typing.
- [ ] 5 MB response check done and recorded in Notes (fallback threshold added if needed).
- [ ] Screenshots through `Capture-Avalonia.ps1` (`-Send -Select Body`, dark and light) of a JSON request body and a JSON
      response, compared with `design/mockups/MainWindow.dc.html`; differences listed in Notes, not committed.
- [ ] `docs/dependencies.md`, ADR 0002 and the task index updated.
- [ ] No Yaapic or stub-server process left running.

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

`feat(avalonia): highlight request and response bodies with LoomEditor`

## Notes
