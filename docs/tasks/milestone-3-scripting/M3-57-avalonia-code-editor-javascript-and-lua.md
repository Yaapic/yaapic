# M3-57: Avalonia: JavaScript and Lua in `CodeEditorView`

Status: todo
Platform: unix
Depends on: M3-03, M3-50

## Goal

The Avalonia twin of M3-50: `CodeEditorView` highlights JavaScript and Lua, nothing else changes.

## Design decisions

- Read first: M3-50 (mapping rule, scope table, the Lua-grammar fallback and its Notes: **reuse the same decisions**),
  `src/Yaapic.App.Avalonia/Views/CodeEditorView.axaml(.cs)`, `Presentation/EditorMap.cs`, the `LoomEditor.Avalonia` `0.5.0` XML
  docs/README in the NuGet cache (`loomeditor.avalonia/0.5.0`) for the bundled grammar ids.
- `EditorLanguage` `"javascript"`/`"lua"` -> `LanguageId` `javascript`/`lua` when shipped, else `plaintext`; add
  `EditorMap.ScriptLanguage(ScriptLanguage)` (twin of `TextMap.ScriptLanguage`); extend the theme scope table copied from
  `ApplyTheme()` with the **same** scope -> token mapping as WinUI (copy it, do not invent a second mapping), colours resolved
  through `BrushMap.Find`.
- The 1 000 000 character plain-text fallback applies unchanged.

## Out of scope

The Scripts tab (M3-58), new colours, autocomplete.

## Acceptance criteria

- [ ] `dotnet build Yaapic.CrossPlatform.slnf -c Release` 0 warnings; `dotnet test --solution Yaapic.CrossPlatform.slnf -c Release`
      green (add a unit test for `EditorMap.ScriptLanguage`).
- [ ] AOT publish of `Yaapic.App.Avalonia` 0 new IL2xxx/IL3xxx (only the two existing `Avalonia.Win32` ILC messages on Windows).
      Runtime (published exe, throwaway fixture not committed): both languages coloured in dark and light, JSON/XML unchanged.
      Record in Notes; if the host is Windows, say that macOS/Linux were not run.
- [ ] No Yaapic or stub-server process left running.

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

`feat(avalonia): highlight JavaScript and Lua in the code editor`
