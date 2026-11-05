# M3-50: WinUI: JavaScript and Lua in `CodeEditorView`

Status: todo
Platform: windows
Depends on: M3-03

## Goal

`CodeEditorView` can highlight JavaScript and Lua (script editors), keeping the `ICodeEditor` contract and all existing
uses (JSON, XML, plain text) unchanged.

## Design decisions

- Read first: `docs/adr/0002-code-editor.md`, `src/Yaapic.App.WinUI/Views/CodeEditorView.xaml(.cs)` (the `EditorLanguage` to
  `LanguageId` mapping, `ApplyTheme()` and its scope table), `Presentation/TextMap.cs`, M2-69 card and Notes, the LoomEditor.WinUI
  `0.5.0` XML docs in the NuGet cache (`%USERPROFILE%\.nuget\packages\loomeditor.winui\0.5.0\lib\...\LoomEditor.WinUI.xml`, and its
  README for the **list of bundled grammars**).
- Mapping: `"javascript"` -> `javascript`, `"lua"` -> `lua` when the package ships those grammar ids (check the exact ids in the
  README/XML or by loading the control with each id and reading the resulting token colours in a scratch run); anything not shipped
  falls back to `plaintext`. `TextMap.Language` (content-type -> language for bodies) stays unchanged; scripts pass the language
  text from the view model: add `TextMap.ScriptLanguage(ScriptLanguage)` returning `"javascript"`/`"lua"`.
- Theme scopes: extend the theme JSON built in code with the scopes the JS/Lua grammars emit, mapped to **existing** tokens
  only: keywords/storage -> `YaapicSyntaxKeyBrush`? **No**: use the tokens defined in `design/tokens` for syntax (`DSSyntaxeIcones.dc.html`
  lists key, string, number, boolean, punctuation, tag, attribute, comment): `keyword.*`/`storage.*` -> tag colour,
  `entity.name.function`/`support.function` -> attribute colour, `variable.*` -> primary text, `constant.language`/`constant.numeric`
  -> number/boolean colours, `string.*` -> string colour, `comment.*` -> comment colour, `keyword.operator`/`punctuation.*` ->
  punctuation colour. Do not add new colours; list the final scope table in Notes and any difference from the mockup.
- If `lua` is **not** shipped by the package: ship plain text for Lua, say so in Notes, and add one line to the milestone summary
  card's open items (do not write a grammar); report it in the card's closing message because it is a product decision.
- Large text rule from M2-70/M2-69 (plain text above 1 000 000 characters) applies to scripts too (shared code path).

## Out of scope

The Scripts tab (M3-51), autocomplete of `pm.*`, linting, new colours, Avalonia (M3-57).

## Acceptance criteria

- [ ] Build `Yaapic.sln` 0 warnings; tests green (add a small unit test for `TextMap.ScriptLanguage`).
- [ ] Runtime (AOT exe of the card, a scratch hosted request body is fine since the Scripts tab does not exist yet: temporarily
      point the response viewer at a JavaScript and a Lua sample through `Capture-WinUI.ps1` fixtures or a throwaway local
      harness, **not committed**): both languages are coloured in dark and light, keywords, strings, numbers and comments
      distinguishable; JSON and XML still coloured; no crash. Record what was run in Notes.
- [ ] AOT publish 0 IL2xxx/IL3xxx; no Yaapic or stub-server process left running.

## Rules

- Every shell command, git included, starts with `rtk` (`rtk bypass <cmd>` only if the condensed output is unusable).
- Wait for long commands (build, test, AOT publish) in the foreground; do not poll.
- Stop every process you launched (published exes, stub servers, `dotnet` build servers you started).
- Check the SonarCloud issues of the files you changed
  (`https://sonarcloud.io/api/issues/search?componentKeys=Yaapic_yaapic&resolved=false`) and fix them before committing.
- Do not skip any acceptance item; if one cannot be met, stop and say why instead of ticking it.
- Commit on `feat/m3-scripting` (no new branch or worktree), GPG-signed
  (`git -c gpg.program="C:\Program Files (x86)\GnuPG\bin\gpg.exe" commit -S`, key of adrien.granger@outlook.com),
  with no trailer or co-author line, then push.

## Suggested commit message

`feat(winui): highlight JavaScript and Lua in the code editor`
