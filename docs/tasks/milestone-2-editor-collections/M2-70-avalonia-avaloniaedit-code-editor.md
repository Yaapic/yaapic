# M2-70: Avalonia: AvaloniaEdit code editor with TextMate grammars

Status: todo
Platform: unix
Depends on: M2-65, M1-57

## Goal

The Avalonia app shows a syntax-highlighted code editor (line numbers, JSON/XML colours from the Yaapic tokens, dark and
light) for the raw request body and the response body, instead of monochrome `TextBox`es, behind the same
`ICodeEditor` abstraction as WinUI (ADR 0002, option 1).

## Design decisions

- Read first: `docs/adr/0002-code-editor.md` ("AOT risks"), `docs/dependencies.md`, the Notes of M1-44,
  `src/Yaapic.UI.Abstractions/Editing/ICodeEditor.cs`, `src/Yaapic.App.WinUI/Views/CodeEditorView.xaml.cs` (behaviour to
  mirror), `src/Yaapic.App.Avalonia/Views/RequestEditorView.axaml` (raw body `TextBox.editor`),
  `Views/ResponsePanelView.axaml` (read-only body `TextBox`) and `Styles/Yaapic.Controls.axaml` (`TextBox.editor`).
- **Packages** (central versions in `Directory.Packages.props`, justified in `docs/dependencies.md`):
  `Avalonia.AvaloniaEdit` and `AvaloniaEdit.TextMate` at the newest stable versions compatible with Avalonia `12.1.4`
  (check nuget.org and each package's dependency list first; if no stable release supports Avalonia 12, use the newest
  prerelease that does and say so in `docs/dependencies.md`; if none works, **stop** and report instead of
  downgrading Avalonia), plus the TextMate grammar package they depend on (`TextMateSharp.Grammars`). Licences
  recorded.
- **AOT constraints** (ADR 0002): compiled bindings only (`AvaloniaUseCompiledBindingsByDefault` is already on) and no
  reflection-based TextMate registry loader. Provide an `IRegistryOptions` implementation in the App project that
  returns **explicit** grammar and theme streams for JSON, XML and plain text: the `json` and `xml` grammar files
  (`*.tmLanguage.json`) bundled as `AvaloniaResource` items under `Assets/Grammars/`, their licences listed in
  `docs/dependencies.md` ("Bundled assets"), and the Yaapic theme (below). Do not use registry options that scan
  assemblies or embedded resources by name. Check that the TextMateSharp stack publishes without IL2xxx/IL3xxx and
  works at run time in the AOT build; if it emits warnings that cannot be avoided, report them instead of suppressing
  them (no `NoWarn`, no `#pragma`).
- **Control**: `src/Yaapic.App.Avalonia/Views/CodeEditorView.axaml(.cs)` (new `UserControl` implementing `ICodeEditor`)
  containing an `AvaloniaEdit.TextEditor` named `Editor`: `ShowLineNumbers` true, `WordWrap` false, `FontFamily`
  `YaapicMonoFont`, `FontSize` `YaapicFontSizeCode`, `Background` `YaapicSurfaceSunkenBrush`, line numbers in
  `YaapicTextDisabledBrush`, selection `YaapicSelectionBackgroundBrush`, no border, padding and line height matching the
  current `TextBox.editor`. Styled properties `Text` (two-way by default), `IsReadOnly` and `EditorLanguage`;
  `ICodeEditor.Language` is implemented explicitly, as in WinUI. `TextEditor.Text` is not bindable, so sync by hand:
  `Editor.TextChanged`/`Document.TextChanged` -> set the `Text` property and raise `TextChanged` for user edits only;
  `Text` changed from outside -> `Editor.Document.Text = value` only if different (guard flag: no echo loop, caret and
  undo stack preserved while typing).
- **Highlighting**: `TextMate.Installation` on the editor with the explicit registry above; scope per `EditorLanguage`:
  `"json"` -> `source.json`, `"xml"` -> `text.xml`, anything else -> no grammar (plain text). Change the grammar when
  `EditorLanguage` changes; dispose the installation when the control is detached.
- **Theme from tokens**: a TextMate theme (VS Code-style JSON built in code from the Yaapic brushes, one for dark and
  one for light, chosen from `ActualThemeVariant`) mapping scopes to `YaapicSyntaxKeyBrush` (JSON keys),
  `YaapicSyntaxStringBrush`, `YaapicSyntaxNumberBrush`, `YaapicSyntaxBooleanBrush` (`true`/`false`/`null`),
  `YaapicSyntaxPunctuationBrush`, `YaapicSyntaxTagBrush`, `YaapicSyntaxAttributeBrush`, `YaapicSyntaxCommentBrush`.
  Colours are read from the application resources for the current theme variant (`TryFindResource` with the variant),
  never copied as hex. Re-apply on `ActualThemeVariantChanged`.
- **Usage**: replace the raw-body `TextBox.editor` in `RequestEditorView.axaml` (`Text` two-way with `BodyText`,
  `EditorLanguage` from the content type through a new converter in
  `src/Yaapic.App.Avalonia/Presentation/EditorMap.cs` that mirrors `TextMap.Language` of WinUI) and the text `TextBox`
  of `ResponsePanelView.axaml` (read-only, `Text` = `BodyText`, `EditorLanguage` = `BodyLanguage`, visible when
  `!IsBinary`). The binary view `TextBox` stays. Remove the `TextBox.editor` style from `Yaapic.Controls.axaml` if
  nothing else uses it. Pretty/Raw switching keeps working.
- **Large text**: add a `/big` route (about 5 MB of JSON) to the stub server of `tools/screenshots/Capture-Avalonia.ps1`
  if there is none; open it in the response editor and check that scrolling and selection stay usable (Pretty and
  Raw). If tokenising 5 MB freezes the UI, add a documented size threshold in `CodeEditorView` above which no grammar
  is installed. Record the measurement and the threshold in Notes.
- **Docs**: `docs/dependencies.md` rows for each new package and bundled grammar (version, licence, used by, why, AOT
  result); `docs/adr/0002-code-editor.md`: a new "Avalonia decision (M2-70)" section (AvaloniaEdit + TextMate behind
  `ICodeEditor`, explicit registry because of AOT, versions) replacing the sentence saying the Avalonia card does not
  exist yet; update `docs/screenshots/m1-avalonia/README.md` only if it lists the editor as a pending follow-up
  (one-line change).

## Out of scope

WinUI (M2-68, M2-69), folding, search UI, autocomplete, `{{variable}}` highlighting, languages beyond JSON, XML and
plain text, the scripts editor, any ViewModel change.

## Acceptance criteria

- [ ] `dotnet build Yaapic.CrossPlatform.slnf -c Release` (and `Yaapic.sln` on Windows) 0 warnings; all tests green;
      architecture tests green (`Yaapic.UI.Abstractions` still free of UI frameworks; the control only syncs text,
      language and theme, no business logic).
- [ ] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings (the existing Avalonia.Win32 ILC
      messages are not new); the published binary edits a JSON body, sends it, shows the coloured JSON response,
      switches Pretty/Raw and exits cleanly; highlighting works in the **AOT** build, not only in the JIT build.
- [ ] JSON and XML are coloured in dark and light with the Yaapic syntax tokens; line numbers visible; switching the
      theme at run time recolours an open editor.
- [ ] Two-way sync verified (typing updates `BodyText` and the unsaved marker; caret stable; tab switch restores text).
- [ ] 5 MB response check recorded in Notes (threshold added if needed).
- [ ] Screenshots with `tools/screenshots/Capture-Avalonia.ps1` (`-Send -Select Body`, dark and light) of a JSON request
      body and response, compared with `design/mockups/MainWindow.dc.html`; differences listed in Notes, not committed.
- [ ] `docs/dependencies.md` and ADR 0002 updated.
- [ ] No Yaapic or stub-server process left running.

## Rules

- Every shell command, git included, starts with `rtk` (`rtk bypass <cmd>` only if the condensed output is unusable).
- Wait for long commands (build, test, AOT publish) in the foreground; do not poll.
- Stop every process you launched (the published binary, the stub server, `dotnet` build servers you started).
- Check the SonarCloud issues of the files you changed
  (`https://sonarcloud.io/api/issues/search?componentKeys=Yaapic_yaapic&resolved=false`) and fix them before committing.
- Commit on `feat/m2-editor-collections` (no new branch or worktree), GPG-signed
  (`git -c gpg.program="C:\Program Files (x86)\GnuPG\bin\gpg.exe" commit -S`, key of adrien.granger@outlook.com),
  with no trailer or co-author line, then push.

## Suggested commit message

`feat(avalonia): highlight request and response bodies with AvaloniaEdit`

## Notes
