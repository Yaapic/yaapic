# M3-55: WinUI: secret variables in the variable tables and popup

Status: done
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

- [x] Build 0 warnings, tests green, AOT publish 0 IL2xxx/IL3xxx.
- [x] **Runtime check (AOT exe)**, workspace with an unlocked passphrase vault created for the test by the composition test
      helper or, once M3-56 exists, through its dialog (if M3-56 is not done, create the vault file with a small throwaway
      `tools/` helper or the composition test and say so in Notes): toggle a row secret, value masks, reveal shows it, save, the
      environment YAML holds `secret: true` and an empty value (open the file), reopen the dialog, value comes back; lock the vault
      (palette command) and the rows show the locked caption; params/headers tables show no secret column; the URL bar popup
      masks a secret variable; dark and light; keyboard focus visible on the new buttons. Record in Notes.
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

`feat(winui): mark variables secret and mask their values`

## Notes

- View model additions (needed by the view, no behaviour change): `KeyValueRowViewModel.IsMasked`, `CanToggleSecret`, `SecretToggleTooltip`;
  `KeyValueEditorViewModel.ShowLockedCaption` and `UnlockCommand` (set from the new `IVaultStateSource.RequestUnlockCommand`, which `VaultViewModel`
  implements). 8 new tests (`SecretRowsTests`).
- A masked secret is a read-only box; revealing it (eye toggle) swaps in the editable cell. A locked secret always shows the mask, disabled.
  The refusal of `ToggleSecretCommand` (`LastError`) and the unflag hint show as caption lines under the row, not in an `InfoBar`
  (the editor view has no `InfoBar`). The lock and eye buttons are `ToggleButton`s with the default checked (accent) look, no new brush.
- The popup shows `DisplayValue`, a lock glyph in the scope badge and a Reveal/Hide link; Edit is hidden for a secret value.
- **Runtime check (AOT exe, win-x64, dark and light)**: M3-56 (vault dialogs) is not done, so a **temporary, uncommitted** line in `App.xaml.cs`
  set up the DPAPI vault at startup (`YAAPIC_TEST_VAULT=1`) and a throwaway script in the session scratchpad drove the app through UI Automation.
  Checked: lock toggle marks the row secret and masks the value, the eye reveals it, Save writes `secret: true` with `value: ""` to
  `local.env.yaml`, reopening the dialog shows the mask and the eye brings the value back; locking the vault (palette) shows the
  `Vault is locked...` caption with `Unlock`, the row masked and disabled; the Headers table has no secret column and no hidden button;
  the URL bar popup shows `••••••••`, the lock glyph and `Reveal`; the lock button shows a focus ring. Patch reverted, processes stopped.
  The test runs left throwaway vault files (random workspace ids) in `%APPDATA%\Yaapicaults`.
