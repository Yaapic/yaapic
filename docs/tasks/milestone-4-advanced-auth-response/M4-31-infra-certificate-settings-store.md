# M4-31: Infrastructure: the client certificate list store

Status: todo
Platform: any
Depends on: M4-29

## Goal

The list of client certificates is persisted per user in `certificates.json` behind a port.

## Design decisions

- Read first: ADR 0014 (Client certificates), ADR 0010 (user data dir rule), the update settings store and its file implementation
  (grep `IUpdateSettingsStore`) and `IVaultLocation`: reuse the same base-folder resolution, do not invent another one.
- Port `ICertificateStore` in `Yaapic.Core.Storage`: `Task<IReadOnlyList<ClientCertificate>> LoadAsync(CancellationToken)`,
  `Task SaveAsync(IReadOnlyList<ClientCertificate>, CancellationToken)`, `event EventHandler? Changed` (raised after a successful save).
  Write failures throw the existing `StorageException`.
- `src/Yaapic.Infrastructure/Storage/FileCertificateStore.cs` (path `<base>/Yaapic/certificates.json`) and `InMemoryCertificateStore` in
  `tests/Yaapic.TestDoubles/`.
- JSON through `System.Text.Json` source generators: `{ "version": 1, "certificates": [ { "id", "name", "hostPattern", "format": "pfx"|"pem",
  "certificatePath", "keyPath", "hasPassphrase" } ] }`, indented, UTF-8 without BOM, written atomically (temp file then move, like the
  vault). A missing file loads as an empty list; a corrupt file or unknown `version` throws `StorageException` with a clear message and
  is left untouched; unknown `format` text drops that entry.
- File mode 0600 on Unix (the paths are not secrets, but the file is per user).

## Out of scope

Loading certificates from the paths (M4-33), passphrases (M4-32), UI.

## Acceptance criteria

- [ ] Tests in `Yaapic.Infrastructure.Tests` (temp folder): round trip with two entries; missing file; corrupt JSON; wrong version; unknown
  format dropped; atomic write (the old file survives when the temp write fails: simulate with a read-only target on the host OS or
  document why not); `Changed` raised once per save; `InMemoryCertificateStore` contract tests.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(infra): store the client certificate list per user`
