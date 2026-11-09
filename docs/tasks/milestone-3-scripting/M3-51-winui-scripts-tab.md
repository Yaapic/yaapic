# M3-51: WinUI: Scripts tab in the request editor

Status: done
Platform: windows
Depends on: M3-39, M3-50

## Goal

The request editor gets a "Scripts" section tab with a pre-request and a post-response editor, a language picker per block and
a count badge, as in the mockup (`Params, Authorization, Headers, Body, Scripts 2, Tests, Settings`).

## Design decisions

- Read first: `design/mockups/MainWindow.dc.html` (`reqTabs`: `['Scripts','2']`), `DSComposants2.dc.html` (panel tabs, segmented
  control), `src/Yaapic.App.WinUI/Views/RequestEditorView.xaml(.cs)` (section `ToggleButton`s `ParamsTab`... and `OnTabClick`, how
  the Settings tab content is shown/hidden), `CodeEditorView`, M3-39 (`Scripts`, `PreRequest`, `PostResponse`, `Languages`,
  `BadgeText`, placeholders), M2-67/M1-45 READMEs (how fidelity is verified).
- Tab strip: add `ScriptsTab` (`YaapicPanelToggleTabStyle`) **after Body and Auth and before Settings**, text `Scripts`, badge bound
  to `ViewModel.Scripts.BadgeText` (visible when not empty, same badge style as Headers). The mockup also shows a `Tests` request
  tab: it is **not** built (tests are written in the post-response script; the response side has the Tests tab, M3-54).
- Content (shown when `ScriptsTab` is checked): a header row with a segmented control `Pre-request | Post-response` (bound to
  `Scripts.SelectedPhase`), a language `ComboBox` bound to `Scripts.Selected.Language` (items from
  `ScriptBlockEditorViewModel.Languages`, label `Label`), a one-line hint (secondary text colour, `YaapicFontSizeSmall`) that
  reads `"Variables, tests and requests: pm.* (JavaScript and Lua)"` plus a link-like text button `Docs` is **not** added; then
  one `CodeEditorView` (Name `ScriptEditor`, `EditorLanguage` from `TextMap.ScriptLanguage(Scripts.Selected.Language)`,
  `Text` two-way bound to `Scripts.Selected.Source`, `AutomationProperties.Name` `"Script"`) filling the pane. A placeholder
  `TextBlock` (`Scripts.Selected.Placeholder`, disabled text colour, monospace, `IsHitTestVisible` false) overlays the editor
  while the source is empty.
- Both phases keep their own text: switching phase swaps the bound block; the editor's undo history is not preserved across
  phases (note it).
- `x:Bind`/compiled bindings only; reuse existing converters in `Presentation/` (add `TextMap.ScriptLanguage`, `VisibilityMap`
  helpers only if none fits); no business logic in the view; styles from the existing resource dictionaries (no new brushes).
- Fixture: add `tools/screenshots/workspace/requests/scripted.request.yaml` (GET `{{baseUrl}}/users`, a pre-request that sets a
  header and variable, a post-response with three `pm.test` lines, one failing on purpose) and register it in
  `Capture-WinUI.ps1` (`$ids`, `-Select Scripts`, same pattern as `-Select Body`).

## Out of scope

Console, tests tab (M3-53, M3-54), folder scripts (M3-52), autocomplete, snippets.

## Acceptance criteria

- [x] `dotnet build Yaapic.sln -c Release` 0 warnings; all tests green; AOT publish 0 IL2xxx/IL3xxx.
- [x] **Runtime check on the AOT exe** (drive the real window, not only the screenshot flag): open `scripted` -> the Scripts tab
      shows `2`; type in pre-request, switch to post-response and back, text kept; switch language to Lua and back; the
      request tab shows the unsaved marker; Ctrl+S saves and the YAML on disk contains the scripts; close and reopen the app, the
      scripts are back; Ctrl+Enter inside the script editor does not send accidentally unless the app's Send shortcut is meant
      to (record the behaviour, as M2-70 did for the body editor); dark and light; keyboard focus visible on the tab and the
      pickers. Record in Notes.
- [x] `Capture-WinUI.ps1 -Select Scripts` produces a screenshot compared with the mockup row of tabs; differences listed in
      Notes (not committed).
- [x] No Yaapic or stub-server process left running.

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

`feat(winui): add the Scripts tab to the request editor`

## Notes

- Runtime check on the AOT exe (dark and light, driven through UI Automation and SendKeys on a copy of the fixture workspace):
  `scripted` opens with `Scripts 2`; text typed in pre-request and post-response is kept when switching phase back and forth;
  Lua and back works from the language picker; the tab shows the unsaved dot and Save enables; Ctrl+S writes both
  `scripts.preRequest` and `scripts.postResponse` to the YAML; after closing and relaunching, the scripts are back.
  Keyboard focus is visible on the tab, the phase control and the language picker.
- Ctrl+Enter inside the script editor does **not** send: the editor consumes it and inserts a new line (same as the body editor).
- Undo history is not preserved across phases (one editor, its text is swapped).
- Card deviations: the hint uses `YaapicFontSizeCaption` (`YaapicFontSizeSmall` does not exist; using it crashes the app at
  startup). The phase and language pickers are index based (enums cannot cross into XAML under Native AOT), like the body kind.
- `FilesWithoutScriptsReadEmptyScripts` now skips the new `scripted` fixture and asserts it carries both scripts (no new test).
- Screenshot compared with the mockup row `Params, Authorization, Headers, Body, Scripts 2, Tests, Settings` (not committed):
  the app has `Auth` instead of `Authorization`, no `Tests` tab (by design), no Headers count on this request; the badge,
  underline and spacing match. The hint is trimmed with an ellipsis at 1440 px.
- No Yaapic process left running.
