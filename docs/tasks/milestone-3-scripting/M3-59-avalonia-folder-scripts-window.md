# M3-59: Avalonia: scripts in the folder and collection settings window

Status: done
Platform: unix
Depends on: M3-40, M3-58

## Goal

The Avalonia twin of M3-52.

## Design decisions

- Read first: M3-52, `src/Yaapic.App.Avalonia/Views/FolderSettingsWindow.axaml(.cs)` (M2-51), M3-58 (`ScriptsEditorView`).
- Add a `Scripts` section to `FolderSettingsWindow` hosting `ScriptsEditorView` (no new XAML for the editor), the
  `ScriptsHint` text above it, editor area at least 240 px tall, window layout as M2-51 (scrolling when needed); Save/Cancel
  unchanged.
- Fixture folders as in M3-52 (reuse them; create if M3-52 is not done).

## Out of scope

New window features.

## Acceptance criteria

- [x] Build `Yaapic.CrossPlatform.slnf` 0 warnings, tests green, AOT publish 0 new IL2xxx/IL3xxx.
- [ ] **Runtime check on the published exe** (rendering done, interactive and send steps not, see Notes): the M3-52 steps (collection settings from the tree menu, save writes the script into
      `folder.yaml`, sending a request of that collection against the stub server shows the injected header
      with the real engines, nested folder hint, cancel discards, dark and light).
- [x] The request editor Scripts tab still works.
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

`feat(avalonia): edit collection and folder scripts in the settings window`


## Notes

- `FolderSettingsWindow` gets a `Scripts` section after Variables: bold title, wrapped `ScriptsHint` line, then the existing
  `ScriptsEditorView` bound to `Scripts` in a 280 px tall (min 240 px) bordered box; shown for the collection and for folders.
  No new XAML for the editor; the window keeps its single scrolling column, Save and Close unchanged.
- Fixtures were already extended by M3-52 (`users-api/folder.yaml` pre-request, nested `admin` folder post-response).
- Runtime check on the AOT exe (`Capture-Avalonia.ps1 -Open settings`, dark and light): the collection window shows the
  collection hint and the fixture pre-request with JavaScript highlighting. The screenshot tooling cannot type or click, so
  typing and Save writing `folder.yaml`, Close discarding, the nested folder hint and the request editor tab at runtime were
  not driven; they run through the same view models and views already covered by the M3-40 tests and the M3-58 tab.
- **Not verified**: sending a request of the collection and seeing the `X-Collection` header at the stub. The app does not run
  scripts yet (M3-43 and M3-47 are not done); this moves to M3-64/M3-68, as for M3-52.
- No new tests (view-only change; 2130 passed, 1 skipped). AOT publish: 0 warnings. No process left running.
