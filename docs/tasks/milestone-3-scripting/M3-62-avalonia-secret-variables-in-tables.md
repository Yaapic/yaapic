# M3-62: Avalonia: secret variables in the variable tables and popup

Status: done
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

- [x] Build `Yaapic.CrossPlatform.slnf` 0 warnings, tests green, AOT publish 0 new IL2xxx/IL3xxx.
- [x] **Runtime check on the published exe**: the M3-55 steps (toggle, mask, reveal, save -> `secret: true` and empty value in the
      YAML, reload restores, lock shows the caption, params/headers unchanged, popup masks, dark and light, focus visible); the
      vault for the test is created as M3-55 describes when M3-63 is not done.
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

`feat(avalonia): mark variables secret and mask their values`

## Notes

- No view model change: M3-55 and the M3-56 fix already expose everything. Avalonia additions: `YaapicGlyphLock` and `YaapicGlyphEye`
  stroke glyphs (the app has no icon package), a `Button.icon.on` style for the two toggles (secret flag, reveal) which are plain
  icon buttons bound to `ToggleSecretCommand` / `ToggleRevealCommand`, so a refused toggle never desynchronises the button.
- `KeyValueEditorView`: the lock column is an `Auto` column holding a 36 px host shown only when `AllowSecrets`; a masked secret is a
  read-only `TextBox` over the editable cell; hint and error captions sit under the row; the locked caption with `Unlock` sits above the rows.
  `VariablePopupView`: `DisplayValue`, lock glyph in the scope badge, Reveal/Hide button, Edit hidden for a secret.
- **Runtime check (AOT exe, win-x64, dark and light)**: M3-63 is not done, so a **temporary, uncommitted** patch (a vault directory
  override in `DefaultVaultLocation` and a vault setup/unlock in `WindowCapture`) pointed the vault at a scratchpad folder, and a throwaway
  UI Automation script drove the app. Checked: the lock toggle marks the row secret and masks the value, the eye reveals it, Save writes
  `secret: true` with `value: ""`, reopening restores the mask and the eye brings the value back; with the vault locked the caption and
  `Unlock` show and the row is masked and disabled; Params has no secret column and no layout shift; the URL bar popup of a secret
  global shows the mask, the lock glyph and Reveal (no Edit); the revealed cell shows a focus ring. Patch reverted, processes stopped,
  nothing written under `%APPDATA%\Yaapic`.
