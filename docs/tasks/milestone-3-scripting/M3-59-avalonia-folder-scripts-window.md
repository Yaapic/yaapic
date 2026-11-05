# M3-59: Avalonia: scripts in the folder and collection settings window

Status: todo
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

- [ ] Build `Yaapic.CrossPlatform.slnf` 0 warnings, tests green, AOT publish 0 new IL2xxx/IL3xxx.
- [ ] **Runtime check on the published exe**: the M3-52 steps (collection settings from the tree menu, save writes the script into
      `folder.yaml`, sending a request of that collection against the stub server shows the injected header
      with the real engines, nested folder hint, cancel discards, dark and light).
- [ ] The request editor Scripts tab still works.
- [ ] No process left running.

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
