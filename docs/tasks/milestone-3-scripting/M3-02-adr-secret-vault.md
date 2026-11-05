# M3-02: ADR 0010: secret vault

Status: todo
Platform: any
Depends on: none

## Goal

Write ADR 0010, the reference for every vault card. Docs only. The decisions below are final.

## Design decisions (to record in the ADR)

- **What is a secret.** A variable row (environment, collection/folder, globals) can be flagged `secret`. Its value is
  never written to the workspace: the YAML row keeps `value: ""` and gains `secret: true`; the value lives in the vault.
  Auth fields (`password`, `token`, API key value) stay plain text as in ADR 0008: the supported way to protect them is
  to write `{{name}}` and define `name` as a secret variable. (A "move to secret variable" helper is a later card.)
- **Where the vault lives.** Per user and per workspace, **outside** the workspace and never in Git:
  `<user data dir>/Yaapic/vaults/<workspaceId>.vault` (`%APPDATA%` on Windows, `~/Library/Application Support` on macOS,
  `$XDG_DATA_HOME` or `~/.local/share` on Linux). `workspaceId` is a GUID stored in `.yaapic/workspace.id` (created on
  first use; the file is git-ignored with the rest of `.yaapic/`). A fresh clone therefore has no secrets: that is the
  intended behaviour (secrets do not travel through Git); sharing secrets between machines is out of scope.
- **Entry identity**: `SecretRef(SecretOwnerKind Kind, Guid OwnerId, string Name)` with kinds `Environment`
  (environment id), `Collection` (id of the top-level folder) and `Globals` (`Guid.Empty`).
- **Cryptography** (all from the .NET framework, `System.Security.Cryptography`, no package, Native AOT safe): a random
  256-bit data key; each value is encrypted with AES-256-GCM, 12-byte random nonce, 16-byte tag, associated data =
  UTF-8 of `yaapic-vault-v1|<kind>|<ownerId>|<name>` (so entries cannot be swapped). File: JSON written with
  `System.Text.Json` source generators: `{ "version": 1, "protector": "<id>", "wrappedKey": "<base64>", "kdf": {...} | null,
  "entries": [ { "kind", "owner", "name", "nonce", "ciphertext", "tag" } ] }`, UTF-8, written atomically (temp file then
  move), file mode 0600 on Unix.
- **Key protection** behind `IKeyProtector` (`Id`, `Wrap(byte[] dataKey)`, `Unwrap(byte[] wrapped)`):
  1. `passphrase` (every platform, the baseline): PBKDF2-HMAC-SHA512, 16-byte random salt, **600 000** iterations
     (stored in `kdf`), key wrapping with AES-256-GCM. The vault is **Locked** until the user types the passphrase.
  2. `dpapi` (Windows): `ProtectedData` with `DataProtectionScope.CurrentUser`; unlocked automatically.
  3. `keychain` (macOS): the data key is stored as a generic password item (service `Yaapic`, account `<workspaceId>`)
     through the Security framework; unlocked automatically.
  4. `secret-service` (Linux): the data key is stored through the freedesktop Secret Service by piping it to
     `secret-tool store` on stdin (never in arguments); if `secret-tool` is absent or the service is unavailable the
     vault falls back to `passphrase`.
  The first creation picks the platform protector when available, otherwise `passphrase`; the setup dialog also offers
  `passphrase` explicitly. The protector id is stored in the file.
- **States**: `NotCreated` (no file), `Locked`, `Unlocked`. Locking forgets the data key and every decrypted value.
  A locked or missing vault makes secret variables **unresolved** (the existing unresolved-variable warning applies and
  the UI shows "Vault is locked"); sends never block on it.
- **Integration**: stores are decorated (`SecretAware*Store`, Core): on load the secret rows get their values from the
  vault, on save the values are written to the vault and blanked in the YAML. Scripts read and write secret variables
  like any variable; `pm.environment.set` on an existing secret row keeps the flag.
- **Leak control**: console entries, test names and messages and script errors are passed through a `SecretRedactor`
  that replaces every known secret value (length >= 4) with `••••`. The key/value editor and the variable popup mask
  secret values (reveal on demand). The history keeps requests with `{{templates}}` (never resolved values), so it holds
  no secret unless the user typed one literally. Response bodies are not redacted (they are the server's data).
- **Accepted limits** (write them in the ADR): decrypted values live as ordinary .NET strings in memory until the vault is
  locked or the app exits; a malicious script can read every secret of the active scopes (scripts are trusted code,
  imported scripts are a risk the import card must warn about); no secret versioning, no audit log.

## Docs to update

New `docs/adr/0010-secret-vault.md` (Status `Accepted`), one line in `docs/adr/README.md`; `docs/file-format.md`: the
`secret: true` row key (environment, folder and globals variable lists) and the workspace id file; `docs/dependencies.md`:
a note that the vault adds **no package** (`System.Security.Cryptography` is part of the framework), except the
`System.Security.Cryptography.ProtectedData` package that M3-48 adds on Windows.

## Out of scope

Code, UI, secret sharing, hardware keys, auth-field secrets.

## Acceptance criteria

- [ ] ADR 0010 contains every decision above and the accepted limits.
- [ ] `docs/file-format.md` documents `secret: true` and `.yaapic/workspace.id`; ADR index and dependencies note updated.
- [ ] No code changed; `dotnet build Yaapic.sln -c Release` still has 0 warnings.

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

`docs(adr): add ADR 0010 secret vault`
