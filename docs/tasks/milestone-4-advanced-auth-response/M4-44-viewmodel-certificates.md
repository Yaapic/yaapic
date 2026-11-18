# M4-44: ViewModel: client certificates settings

Status: done
Platform: any
Depends on: M4-31, M4-32

## Goal

A view model lists, adds, edits and removes client certificate entries, including their passphrase in the vault.

## Design decisions

- Read first: ADR 0014, `src/Yaapic.ViewModels/Settings/SettingsViewModel.cs` and `Vault/` view models (`VaultViewModel`: how the vault state and
  unlock are exposed), `IDialogService.PickFileAsync`, `ConfirmDeleteAsync`.
- `CertificatesViewModel(ICertificateStore store, ISecretVault vault, IDialogService dialogs)` in `Yaapic.ViewModels.Settings`:
  - `Items` (`ObservableCollection<CertificateItemViewModel>`), `Selected`, `LoadAsync()` (from the store), commands `AddCommand` (new entry
    named from `Strings.CertificateNewName`, `Pfx`, empty fields, selected), `RemoveCommand` (confirmation through `ConfirmDeleteAsync`, removes the
    entry, then `vault.RemoveOwnerAsync(Certificate, id)` when the vault is unlocked), `SaveCommand`, `BrowseCertificateCommand` and
    `BrowseKeyCommand` (use `PickFileAsync`, set the path), `MoveUpCommand`/`MoveDownCommand` (order matters for ties, ADR 0014).
  - `CertificateItemViewModel`: editable `Name`, `HostPattern`, `Format`, `CertificatePath`, `KeyPath`, `Passphrase` (write-only: a new text
    to store; empty means "unchanged"), `HasPassphrase`, `Issues` (from `ClientCertificateValidator`, localized text list), `IsKeyPathVisible`
    (`Pem`).
  - Save: validates every entry (invalid entries block the save and show their issues), writes the list through the store, and for each entry with
    a new passphrase calls `vault.SetAsync(SecretRef.Certificate(id), text)`; when the vault is not `Unlocked` and a passphrase was typed the
    save does **not** write anything and shows `Strings.CertificateVaultLockedForPassphrase`. An entry whose "Remove passphrase" flag was set
    calls `vault.RemoveAsync`. `HasPassphrase` is true after a successful set.
  - `VaultState` is exposed (bound to the vault's `StateChanged`) so the view can say "Vault locked".
  - Dirty tracking: `IsDirty` true after an edit, false after save or load.
- Strings (EN and FR, regenerate `Strings.g.cs`): `CertificatesTitle`, `CertificateNewName`, field labels, the issue texts (one per
  `ClientCertificateIssue`), `CertificateVaultLockedForPassphrase`, `CertificateRemovePassphrase`.

## Out of scope

Views (M4-64, M4-73), the shell entry (M4-60, M4-61), testing a connection.

## Acceptance criteria

- [x] Tests in `Yaapic.ViewModels.Tests` with `InMemoryCertificateStore`, `InMemorySecretVault`, `FakeDialogService`: add, edit, save and reload;
  invalid entries block save and list issues; passphrase stored in the vault and never in the store's data; locked vault with a typed passphrase
  blocks save with the message; remove deletes the vault secret; order buttons; browse commands set paths; dirty tracking; `Passphrase`
  text is cleared after save.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(viewmodels): add the client certificates settings view model`
