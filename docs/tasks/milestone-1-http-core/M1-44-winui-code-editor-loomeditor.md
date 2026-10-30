# M1-44: WinUI code editor: evaluate and adopt LoomEditor.WinUI

Status: todo
Platform: windows
Depends on: M1-42

## Goal

Replace the plain `TextBox`es of the request body and the response body with a code editor (line numbers, syntax
colours from the Yaapic tokens) like the mockups (`design/mockups/MainWindow.dc.html`: 44 px gutter, fold markers,
JSON colours), using `LoomEditor.WinUI` if it passes the Native AOT check. This confirms ADR 0002 for WinUI.

## Design decisions

- **Candidate**: NuGet `LoomEditor.WinUI` `0.2.0-alpha.5` (WinUI only; the user's preferred control). First read its
  package (nuget.org page, `.nupkg` content: README, XML docs, target frameworks, dependencies, licence) and record the
  findings. Reject it (and keep `TextBox`, see below) if any of these fails: the package resolves for
  `net11.0-windows10.0.19041.0` with the current Windows App SDK; licence compatible with the project; AOT publish
  with 0 IL2xxx/IL3xxx warnings; the published exe shows and edits text in the editor without crashing; text can be
  read and written from code (for two-way sync); read-only mode exists.
- **Abstraction** (ADR 0002): `src/Yaapic.UI.Abstractions/Editing/ICodeEditor.cs` with `string Text { get; set; }`,
  `bool IsReadOnly { get; set; }`, `string Language { get; set; }` (`"json"`, `"xml"`, `"text"`, the values of
  `ResponsePanelViewModel.BodyLanguage`) and `event EventHandler? TextChanged`. Full XML docs. No WinUI type in it.
- **WinUI control**: `src/Yaapic.App.WinUI/Views/CodeEditorView.xaml` (`UserControl`, implements `ICodeEditor`)
  with dependency properties `Text` (two-way capable, no update loop: ignore the echo of its own change), `IsReadOnly`
  and `Language`. Inside: the LoomEditor control, or the borderless `TextBox` fallback. Visuals from tokens:
  background `YaapicSurfaceSunkenBrush`, font `YaapicMonoFont` 12/20, gutter 44 px with line numbers in
  `YaapicTextDisabledBrush`, current line and selection `YaapicSelectionBackgroundBrush`, syntax colours
  `YaapicSyntaxKeyBrush` (keys), `YaapicSyntaxStringBrush`, `YaapicSyntaxNumberBrush`, `YaapicSyntaxBooleanBrush`
  (true/false/null), `YaapicSyntaxPunctuationBrush`, `YaapicSyntaxTagBrush` / `YaapicSyntaxAttributeBrush` for XML;
  theme changes must recolour the editor (listen to `ActualThemeChanged` if the control needs explicit colours).
- **Usage**: the request body (`RequestEditorView`, two-way with `BodyText`, `Language` from the content type) and the
  response body (`ResponsePanelView`, read-only, `Text` = `BodyText`, `Language` = `BodyLanguage`; the binary
  `TextBox` stays as is). Pretty/Raw switching keeps working.
- **Large text**: open a 5 MB JSON response through the M1-34 stub (add a `/big` route to the script if needed) and
  check scrolling stays usable; note the result.
- **Docs**: `docs/dependencies.md` row for `LoomEditor.WinUI` (version, licence, `Yaapic.App.WinUI`, justification, AOT
  check result) or, if rejected, a short "Rejected: LoomEditor.WinUI" section with the reason; ADR 0002 updated:
  status `Accepted`, WinUI decision (LoomEditor or `TextBox` fallback behind `ICodeEditor`), and that Avalonia still
  uses AvaloniaEdit later (its card is not written yet). `Directory.Packages.props` gets the version if adopted.
- If rejected: `CodeEditorView` wraps the borderless `TextBox` (no line numbers, no colours), the views still use
  `CodeEditorView`, and the card's Notes say what blocked.

## Out of scope

Folding UI if the control lacks it, variable highlighting (`{{var}}` chips), autocomplete, search, minimap, scripts
editor, Avalonia editor.

## Acceptance criteria

- [ ] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green; architecture tests green
      (`Yaapic.UI.Abstractions` still references no UI framework).
- [ ] AOT publish of `Yaapic.App.WinUI` 0 IL2xxx/IL3xxx warnings; published exe starts, edits a body, sends, shows
      the response in the editor, and exits cleanly.
- [ ] Screenshots (M1-34 script with `-Send`) dark and light of a JSON request body and a JSON response inspected
      against `MainWindow.dc.html`; differences listed in the card's Notes, not committed.
- [ ] `docs/dependencies.md` and `docs/adr/0002-code-editor.md` updated.

## Suggested commit message

`feat(winui): add the code editor for request and response bodies`
