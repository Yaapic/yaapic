# M3-52: WinUI: scripts in the folder and collection settings dialog

Status: done
Platform: windows
Depends on: M3-40, M3-50, M3-51

## Goal

The folder/collection settings dialog gets a Scripts section so collection-level and folder-level scripts can be written.

## Design decisions

- Read first: `src/Yaapic.App.WinUI/Views/FolderSettingsDialog.xaml(.cs)` (M2-48: sections, how variables are hosted), M3-40
  (`Scripts`, `ShowScripts`, `ScriptsHint`), M3-51 (the Scripts tab content to reuse).
- Extract the Scripts tab content of M3-51 into a reusable `Views/ScriptsEditorView.xaml(.cs)` (`UserControl`, `ViewModel` of type
  `ScriptsEditorViewModel`: phase switch, language picker, `CodeEditorView`, placeholder overlay) and make both the request editor
  and the dialog use it (**no duplicated XAML**); the request editor change is a pure refactor, its behaviour and tests stay the same.
- Dialog: add a `Scripts` section below Variables (or a `SelectorBar`/tabs `General | Variables | Scripts` if the dialog already
  uses sections: follow what M2-48 did), the hint text from `ScriptsHint` above the editor, the editor area at least 240 px tall;
  the dialog grows to max 80 % of the window height with scrolling as it already does. The primary button stays "Save".
- Fixture: extend `tools/screenshots/workspace` collection folder (`users-api/folder.yaml` and a nested folder) with a
  pre-request on the collection (`pm.request.headers.add({key: "X-Collection", value: "1"})`) and a post-response on a folder.

## Out of scope

New dialog features, Avalonia (M3-59), script libraries.

## Acceptance criteria

- [x] Build 0 warnings, tests green, AOT publish 0 IL2xxx/IL3xxx.
- [ ] **Runtime check (AOT exe)** (all but the final send/header step done, see Notes): open the collection settings from the tree context menu -> Scripts section shows the
      collection hint; type a pre-request, save; `folder.yaml` on disk has the script; open a request of that collection, send
      it against the stub server and verify the header arrives (the stub echoes headers) — the full chain works with real
      engines; a nested folder shows the folder hint; cancel discards edits; dark and light.
- [x] Request editor Scripts tab still works after the extraction (re-run the M3-51 runtime steps briefly).
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

`feat(winui): edit collection and folder scripts in the settings dialog`

## Notes

- `Views/ScriptsEditorView` (phase switch, language picker, `CodeEditorView`, placeholder; `ViewModel` and `HintText` dependency
  properties) is shared by the request editor tab and the dialog; the request editor change is a pure refactor.
- The dialog has no tabs (M2-48 uses one scrolling column), so a `Scripts` section follows Variables: hint from `ScriptsHint`
  in its own wrapped line, editor 280 px tall (the pickers row hint is blank there). Shown for the collection and for folders.
- Runtime check on the AOT exe (fixture copy, dark and light): the collection dialog shows the collection hint and the
  fixture pre-request, the nested `Admin` folder shows the folder hint; typing a second pre-request line and Save writes
  `scripts.preRequest` to `folder.yaml`; Esc discards typed edits (file unchanged, reopening shows the saved text); the
  request editor Scripts tab still shows `Scripts 2` and its editor.
- **Not verified**: sending a request of the collection and seeing the `X-Collection` header at the stub. The app does not run
  scripts yet (send integration M3-43 and composition M3-47 are not done: even the request's own `X-Trace` pre-request
  does not reach the stub), so this step moves to M3-64/M3-68.
- No new tests (view-only change; the folder fixtures now carry scripts and the existing tests pass: 2116 passed, 1 skipped).
- No Yaapic or stub-server process left running.
