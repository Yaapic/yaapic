# M4-64: WinUI: client certificates dialog

Status: todo
Platform: windows
Depends on: M4-44, M4-61

## Goal

A dialog to manage the client certificates (mTLS), opened from the palette.

## Design decisions

- Read first: `Views/VaultDialog.xaml(.cs)` and `EnvironmentsDialog.xaml(.cs)` (a master-detail dialog and how the shell opens dialogs: the `WinUIDialogService`,
  `DialogButtons`), M4-44, M4-60.
- `Views/CertificatesDialog.xaml` (`ContentDialog`-style like the others, `CertificatesViewModel`): left a `ListView` of entries (name, host pattern; an icon-free two-line
  row) with Add, Remove, Move up and Move down buttons; right the editor of the selected entry: Name, Host pattern (placeholder `api.example.com or *.example.com:8443`),
  Format `ComboBox` (PFX, PEM), Certificate file (`TextBox` + Browse button), Private key file (+ Browse; shown only for PEM), Passphrase (`PasswordBox`, placeholder
  `Leave empty to keep the current one` when `HasPassphrase`), a "Remove passphrase" `CheckBox` when `HasPassphrase`; the `Issues` list in the warning style; a vault-state line
  (`Strings.VaultLocked` style: reuse the existing localized "Vault is locked" strings and an **Unlock** button that opens the existing vault dialog); Save and Close buttons
  (Save disabled when `!IsDirty`; closing with unsaved changes asks through `ConfirmUnsavedAsync`).
- `x:Bind` only; control styles of M1-36/M1-37; tokens only.
- Handle the shell event `CertificatesRequested` in the App (where `VaultDialog` is handled): one line plus the dialog class.

## Out of scope

A "test connection" button, importing certificates into the OS store.

## Acceptance criteria

- [ ] AOT publish of `Yaapic.App.WinUI`: 0 IL2xxx/IL3xxx warnings; the exe starts (kill it afterwards).
- [ ] Manual smoke on the published exe: add a PFX entry for `localhost`, store its passphrase with the vault unlocked, save, reopen the dialog (entry and the "has passphrase"
  state back, no passphrase text shown), send to a local mTLS stub (the test project's stub server started by hand with a client certificate requirement: describe how) and see the
  response; with the vault locked the send reports the vault message; remove the entry. Record anything not run.
- [ ] Strings in EN and FR; 0 warnings, all tests green.

## Suggested commit message

`feat(winui): add the client certificates dialog`
