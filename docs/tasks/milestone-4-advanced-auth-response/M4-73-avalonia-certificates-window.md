# M4-73: Avalonia: client certificates window

Status: done
Platform: unix
Depends on: M4-44, M4-61

## Goal

The Avalonia window to manage client certificates, matching M4-64.

## Design decisions

- Read first: M4-64 (the layout and behaviour are identical), `Views/VaultWindow.axaml(.cs)`, `Views/EnvironmentsWindow.axaml(.cs)` (master-detail window), `AvaloniaDialogService.cs`
  (`PickFileAsync`: it must work on this front-end; if it still returns `null` by default, implement it with `StorageProvider.OpenFilePickerAsync` as part of this card and say so).
- `Views/CertificatesWindow.axaml` (`CertificatesViewModel`, compiled bindings): list with Add, Remove, Move up, Move down; editor with Name, Host pattern, Format `ComboBox`, certificate and
  key file `TextBox` + Browse buttons (key row visible for PEM), Passphrase masked field, Remove passphrase `CheckBox`, issues list, vault-state line with an Unlock button opening the vault
  window, Save and Close with the unsaved-changes prompt.
- Opened from `CertificatesRequested` (M4-60).

## Out of scope

Testing a connection, the OS certificate store.

## Acceptance criteria

- [x] AOT publish of `Yaapic.App.Avalonia` for the host RID: 0 IL2xxx/IL3xxx warnings; the exe starts and exits cleanly (kill it afterwards).
- [ ] Runtime smoke on macOS or Linux as in M4-64 (add, save, reopen, send to a local mTLS stub, locked-vault message); if not possible, say so in the Notes and tick only the build and AOT
  items.
- [x] Strings in EN and FR; 0 warnings, all tests green.

## Suggested commit message

`feat(avalonia): add the client certificates window`

## Notes

- `AvaloniaDialogService.PickFileAsync` already used `StorageProvider.OpenFilePickerAsync`; nothing to change there.
- The dialog service owner is now the top-most modal window (`App.TopWindow`), so the delete confirmation shows over the certificates window instead of behind it.
- Added the `YaapicGlyphArrowUp`, `YaapicGlyphArrowDown` and `YaapicGlyphDelete` glyphs.
- Not done: the runtime smoke on macOS or Linux (add, save, reopen, mTLS stub, locked vault); this card was built on Windows, so only the build, tests and AOT items are ticked. The AOT exe started and exited cleanly.
