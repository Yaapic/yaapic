# M3-62: Avalonia: secret variables in the variable tables and popup

Status: todo
Platform: unix
Depends on: M3-46, M3-47, M3-55

## Goal

The Avalonia twin of M3-55.

## Design decisions

- Read first: M3-55 (column, masking, reveal, unflag hint, locked caption, popup behaviour, what is unchanged),
  `src/Yaapic.App.Avalonia/Views/KeyValueEditorView.axaml(.cs)`, `EnvironmentsWindow.axaml`, `FolderSettingsWindow.axaml`,
  `VariablePopupView.axaml(.cs)`, M3-46.
- Same behaviour: lock `ToggleButton` column only when `AllowSecrets`, value cell masked through `DisplayValue` with an eye
  toggle (`IsRevealed`), disabled when locked, caption row with `Unlock` text button, popup masking and `Reveal`. Use the existing
  icon set (the FluentIcons/Avalonia equivalent the app already uses) and styles; compiled bindings; no layout shift for editors
  without secrets (params, headers).

## Out of scope

Vault windows (M3-63), secret creation from the popup.

## Acceptance criteria

- [ ] Build `Yaapic.CrossPlatform.slnf` 0 warnings, tests green, AOT publish 0 new IL2xxx/IL3xxx.
- [ ] **Runtime check on the published exe**: the M3-55 steps (toggle, mask, reveal, save -> `secret: true` and empty value in the
      YAML, reload restores, lock shows the caption, params/headers unchanged, popup masks, dark and light, focus visible); the
      vault for the test is created as M3-55 describes when M3-63 is not done.
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

`feat(avalonia): mark variables secret and mask their values`
