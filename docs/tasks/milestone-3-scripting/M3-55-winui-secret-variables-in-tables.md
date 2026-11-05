# M3-55: WinUI: secret variables in the variable tables and popup

Status: todo
Platform: windows
Depends on: M3-46, M3-47

## Goal

The environment, globals and folder variable tables show a secret toggle and masked values with a reveal button; the inline
variable popup masks secret values.

## Design decisions

- Read first: `src/Yaapic.App.WinUI/Views/KeyValueEditorView.xaml(.cs)` (columns, enabled checkbox, delete button),
  `EnvironmentsDialog.xaml`, `FolderSettingsDialog.xaml`, `VariablePopupView.xaml(.cs)`, `design/mockups/EnvVars.dc.html` (variables
  table), M3-46 (`AllowSecrets`, `SecretsAvailable`, `IsSecret`, `DisplayValue`, `ToggleSecretCommand`, `ToggleRevealCommand`,
  `CanEditValue`).
- `KeyValueEditorView`: when `ViewModel.AllowSecrets` a narrow column (before the delete button) holds a lock `ToggleButton`
  (FluentIcons lock icon, `AutomationProperties.Name` `"Secret"`, tooltip `"Store this value in the vault"`; disabled with tooltip
  `"Unlock the vault to use secrets"` when `!SecretsAvailable` and the row is not already secret) bound to `IsSecret`
  (via `ToggleSecretCommand`, which can refuse: show `LastError` text in the existing error `InfoBar`). The value cell shows
  `DisplayValue` in a read-only `TextBlock`-like mode **or** a `PasswordBox` when secret and not revealed: use a `TextBox`
  with the value hidden by `DisplayValue` and an eye `ToggleButton` (`IsRevealed`) at the right of the cell; editing a secret
  value requires `CanEditValue`. Rows of `AllowSecrets == false` editors are unchanged (no extra column, no layout shift).
- The unflag hint (`"This value will be stored in the workspace file."`) appears as a small caption under the row until the next edit.
- `VariablePopupView`: value text uses `DisplayValue`; when `IsSecret` an `Reveal` text button (toggle) and a lock glyph next to
  the scope label; the unresolved/create form is unchanged.
- Locked vault: secret rows show `••••` disabled and a caption row at the top of the table `"Vault is locked. Unlock it to view or
  edit secret values."` with an `Unlock` text button raising `Vault.RequestUnlockCommand` (the dialog is M3-56).
- Compiled bindings, existing styles, no new brushes.

## Out of scope

The vault dialogs (M3-56), creating a secret from the popup, copying secret values.

## Acceptance criteria

- [ ] Build 0 warnings, tests green, AOT publish 0 IL2xxx/IL3xxx.
- [ ] **Runtime check (AOT exe)**, workspace with an unlocked passphrase vault created for the test by the composition test
      helper or, once M3-56 exists, through its dialog (if M3-56 is not done, create the vault file with a small throwaway
      `tools/` helper or the composition test and say so in Notes): toggle a row secret, value masks, reveal shows it, save, the
      environment YAML holds `secret: true` and an empty value (open the file), reopen the dialog, value comes back; lock the vault
      (palette command) and the rows show the locked caption; params/headers tables show no secret column; the URL bar popup
      masks a secret variable; dark and light; keyboard focus visible on the new buttons. Record in Notes.
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

`feat(winui): mark variables secret and mask their values`
