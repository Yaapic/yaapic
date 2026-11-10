# ADR 0010: Secret vault

Status: Accepted

See also: [ADR 0005](0005-storage-format.md), [ADR 0008](0008-collections-globals-history-storage.md),
[ADR 0009](0009-scripting.md). Every vault card reads this ADR first.

## Context

Variables (environment, collection and globals) are stored in clear in the workspace, which is meant to be committed to
Git. Users need values (tokens, passwords, API keys) that never reach the workspace files, the history, the console or
a test report. This ADR fixes what a secret is, where the vault lives, how it is encrypted and unlocked, how it plugs
into the stores and scripts, and its accepted limits.

## Decision

### What is a secret

A variable row (environment, collection/folder, globals) can be flagged `secret`. Its value is never written to the
workspace: the YAML row keeps `value: ""` and gains `secret: true`; the value lives in the vault. Auth fields
(`password`, `token`, API key value) stay plain text as in ADR 0008: the supported way to protect them is to write
`{{name}}` and define `name` as a secret variable. (A "move to secret variable" helper is a later card.)

### Where the vault lives

Per user and per workspace, **outside** the workspace and never in Git:
`<user data dir>/Yaapic/vaults/<workspaceId>.vault` (`%APPDATA%` on Windows, `~/Library/Application Support` on macOS,
`$XDG_DATA_HOME` or `~/.local/share` on Linux). `workspaceId` is a GUID stored in `.yaapic/workspace.id` (created on
first use; the file is git-ignored with the rest of `.yaapic/`). A fresh clone therefore has no secrets: that is the
intended behaviour (secrets do not travel through Git). Sharing secrets between machines is out of scope.

### Entry identity

`SecretRef(SecretOwnerKind Kind, Guid OwnerId, string Name)` with kinds `Environment` (environment id), `Collection`
(id of the top-level folder) and `Globals` (`Guid.Empty`).

### Cryptography

Everything comes from the .NET framework (`System.Security.Cryptography`): no package, Native AOT safe.

- A random 256-bit data key.
- Each value is encrypted with AES-256-GCM: 12-byte random nonce, 16-byte tag, associated data = UTF-8 of
  `yaapic-vault-v1|<kind>|<ownerId>|<name>` (so entries cannot be swapped).
- File: JSON written with `System.Text.Json` source generators, UTF-8:
  `{ "version": 1, "protector": "<id>", "wrappedKey": "<base64>", "kdf": {...} | null,
  "entries": [ { "kind", "owner", "name", "nonce", "ciphertext", "tag" } ] }`.
- Written atomically (temp file, then move); file mode 0600 on Unix.

### Key protection

Behind `IKeyProtector` (`Id`, `Wrap(byte[] dataKey)`, `Unwrap(byte[] wrapped)`):

1. `passphrase` (every platform, the baseline): PBKDF2-HMAC-SHA512, 16-byte random salt, **600 000** iterations (stored
   in `kdf`), key wrapping with AES-256-GCM. The vault is **Locked** until the user types the passphrase.
2. `dpapi` (Windows): `ProtectedData` with `DataProtectionScope.CurrentUser`; unlocked automatically.
3. `keychain` (macOS): the data key is stored as a generic password item (service `Yaapic`, account `<workspaceId>`)
   through the Security framework; unlocked automatically.
4. `secret-service` (Linux): the data key is stored through the freedesktop Secret Service by piping it to
   `secret-tool store` on stdin (never in arguments); if `secret-tool` is absent or the service is unavailable the vault
   falls back to `passphrase`.

The first creation picks the platform protector when available, otherwise `passphrase`; the setup dialog also offers
`passphrase` explicitly. The protector id is stored in the file.

### States

`NotCreated` (no file), `Locked`, `Unlocked`. Locking forgets the data key and every decrypted value. A locked or
missing vault makes secret variables **unresolved** (the existing unresolved-variable warning applies and the UI shows
"Vault is locked"); sends never block on it.

### Integration

Stores are decorated (`SecretAware*Store`, Core): on load the secret rows get their values from the vault, on save the
values are written to the vault and blanked in the YAML. Scripts read and write secret variables like any variable;
`pm.environment.set` on an existing secret row keeps the flag.

### Leak control

- Console entries, test names and messages and script errors pass through a `SecretRedactor` that replaces every known
  secret value (length >= 4) with `••••`.
- The key/value editor and the variable popup mask secret values (reveal on demand).
- The history keeps requests with `{{templates}}` (never resolved values), so it holds no secret unless the user typed
  one literally.
- Response bodies are not redacted (they are the server's data).

## Accepted limits

- Decrypted values live as ordinary .NET strings in memory until the vault is locked or the app exits.
- A malicious script can read every secret of the active scopes: scripts are trusted code, and imported scripts are a
  risk the import card must warn about.
- Redaction matches values as written: a secret printed base64- or URL-encoded is not detected.
- No secret versioning, no audit log.
- Out of scope: secret sharing, hardware keys, secrets in auth fields.

## Native AOT

Only `System.Security.Cryptography` (AES-GCM, PBKDF2, `ProtectedData` on Windows), `System.Text.Json` source generators
and `Process` (for `secret-tool`) are used: no reflection, no new package except `System.Security.Cryptography.ProtectedData`
on Windows (M3-48).

## Implementation notes (M3)

Deviations and details found while building milestone 3 (read from the notes of the M3 cards and checked against the code):

- **`IKeyProtector`.** The port is `Id`, `RequiresPassphrase`, `IsAvailable(out reason)`, `Wrap(ReadOnlySpan<byte> dataKey,
  string workspaceId)` returning a `WrappedKey(Bytes, Kdf)` and `Unwrap(WrappedKey, string workspaceId)`, not `byte[]` in and out;
  the KDF parameters travel with the wrapped key. `IKeyProtectorFactory` lists the available protectors (platform first,
  passphrase last) and creates one by id.
- **File.** The `kdf` object is `{ "algorithm": "pbkdf2-sha512", "iterations": 600000, "salt": "<base64>" }` (16-byte salt) and is
  `null` for the other protectors; `kind` is the enum name and `owner` a "D" GUID. Reading refuses a different `version`.
- **DPAPI** (M3-48). `ProtectedData` with `DataProtectionScope.CurrentUser`; off Windows `IsAvailable` is false and `Wrap`/`Unwrap`
  throw `VaultException`. A Windows build of either app offers DPAPI and the passphrase.
- **Keychain** (M3-49). The legacy `SecKeychainAddGenericPassword`, `SecKeychainFindGenericPassword`, `SecKeychainItemDelete` and
  `SecKeychainItemFreeContent` functions (plus `CFRelease`) are used instead of the CFDictionary-based `SecItem*` API, which
  needs far more CoreFoundation P/Invokes. The wrapped key bytes are the markers `keychain-v1` and `secret-service-v1`.
  `ProcessSecretTool` runs `secret-tool` with `ArgumentList` (key on stdin only, 5 s timeout). `Yaapic.Infrastructure` has
  `AllowUnsafeBlocks` for the `LibraryImport` generator.
- **Wiring.** `AppComposition.CreateShell` takes the platform `IKeyProtectorFactory`: the WinUI app and the Avalonia app on
  Windows pass `DpapiKeyProtectorFactory`, the Avalonia app elsewhere passes `UnixKeyProtectorFactory`; with none the
  passphrase protector is the only choice. The Secret Service fallback to `passphrase` is the factory's job.
- **Redaction.** `SecretRedactor` matches values as written, case-sensitive, longest first, for values of at least 4
  characters; it is applied to console entries, test names and messages and script errors by `ScriptedRequestSender`.
- **Defect fixed in M3-56.** `RequestEditorViewModel.UsesLockedSecret` only looked at the URL, raw body and headers, so a
  locked secret used in a query parameter, an auth field or a form field was sent empty without the "Vault is locked"
  warning. It now checks every text the resolver substitutes.
- **Not run on the target OS.** The real macOS Keychain path and the real Linux `secret-tool` path (both need a manual run), and
  the Avalonia vault runtime check of M3-63 (setup, masking, restart, lock, wrong passphrase).
- **Auth fields.** Auth fields stay plain text (as decided); a secret variable referenced as `{{name}}` is the supported way.
