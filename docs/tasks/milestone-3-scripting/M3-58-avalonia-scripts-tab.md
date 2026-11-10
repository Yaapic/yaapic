# M3-58: Avalonia: Scripts tab in the request editor

Status: done
Platform: unix
Depends on: M3-39, M3-57, M3-51

## Goal

The Avalonia twin of M3-51, built as a reusable `ScriptsEditorView` from the start (also used by M3-59).

## Design decisions

- Read first: M3-51 (layout, texts, badge, placeholder overlay, fixture request, `-Select Scripts` switch: **mirror all of it**),
  `src/Yaapic.App.Avalonia/Views/RequestEditorView.axaml(.cs)` (`TabControl Classes="panel"`, `TabItem` headers with badges, the
  tunnelling `KeyDown` handler added by M2-70 for Ctrl+Enter), `Views/CodeEditorView`, `Styles/`, `Presentation/` converters,
  `tools/screenshots/Capture-Avalonia.ps1`.
- `Views/ScriptsEditorView.axaml(.cs)` (`UserControl`, `x:DataType` `ScriptsEditorViewModel`): segmented `ListBox`
  (`Classes="segmented"`, as the body kind switch) `Pre-request | Post-response` bound to `SelectedPhase` through an
  index/converter in `Presentation/EditorMap.cs`-style static helper; language `ComboBox` (`Languages`, `Label`); hint text;
  `CodeEditorView` (`EditorLanguage` from `EditorMap.ScriptLanguage`, `Text` two-way with `Selected.Source`,
  `AutomationProperties.Name` `"Script"`); placeholder overlay `TextBlock`.
- Request editor: a `TabItem` `Scripts` **after Auth and before Settings** with the badge (`Scripts.BadgeText`, same style as the
  Headers badge) containing `ScriptsEditorView`.
- Fixture: reuse `tools/screenshots/workspace/requests/scripted.request.yaml` created by M3-51 (if absent because M3-51 is not
  done yet, create it as described there); add the `scripted` id and `-Select Scripts` to `Capture-Avalonia.ps1`.
- Ctrl+Enter inside the script editor: the M2-70 tunnelling handler sends; keep that behaviour and record it.

## Out of scope

Console, tests tab, folder scripts window (M3-59).

## Acceptance criteria

- [x] `dotnet build Yaapic.CrossPlatform.slnf -c Release` 0 warnings; tests green; AOT publish 0 new IL2xxx/IL3xxx.
- [x] **Runtime check on the published exe** (drive the real window): the same steps as M3-51 (badge `2`, type in both phases,
      language switch, unsaved marker, Ctrl+S persists to YAML, reopen restores, dark and light, keyboard focus visible).
      Record in Notes; macOS/Linux not run unless the host is that OS.
- [x] `Capture-Avalonia.ps1 -Send -Select Scripts` screenshot compared with the mockup; differences in Notes (not committed).
- [x] No process left running.

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

`feat(avalonia): add the Scripts tab to the request editor`

## Notes

- Runtime check on the AOT exe (Windows, driven through UI Automation, SendKeys and mouse on a copy of the fixture workspace): `scripted`
  opens with `Scripts 2`; text typed in both phases is kept when switching phase; Lua and back works from the language picker; the tab
  and the request header show the unsaved dot; Ctrl+S writes both `scripts.preRequest` and `scripts.postResponse` to the YAML; after a
  relaunch the scripts and the badge are back. Dark and light screenshots checked; keyboard focus ring seen on the tab and pickers in
  the screenshots only. macOS and Linux not run (host is Windows).
- No overlay: an empty block shows its commented template as real editor text (`ScriptBlockEditorViewModel`, see da016b9).
- Ctrl+Enter inside the script editor is caught by the M2-70 tunnelling handler of `RequestEditorView` (sends), as for the body editor.
- Undo history is not preserved across phases (one editor, its text is swapped).
- The pickers are index based (`EditorMap.ScriptPhaseIndex`, `ScriptLanguageIndex`); the pure helpers are unit-tested (4 cases).
- Screenshot compared with the mockup row (not committed): `Auth` instead of `Authorization`, no `Tests` tab (by design); badge,
  underline and spacing match.
