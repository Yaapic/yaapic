# M3-66: End-to-end tests: secret vault

Status: todo
Platform: any
Depends on: M3-47, M3-64

## Goal

Prove the vault end to end: files never contain secrets, secrets resolve when sending, scripts can use them, output is redacted,
and lock/unlock behave.

## Design decisions

- `tests/Yaapic.Infrastructure.Tests/EndToEnd/Milestone3VaultEndToEndTests.cs`, real files in a `TempWorkspace`, a test
  `IVaultLocation` in a temp dir (never the real user directory), the **passphrase** protector (fast iteration override from M3-36 through
  the composition test seam), real engines for the script scenarios (same native-library rule as M3-64), loopback server.
- Scenarios:
  1. **At rest**: create the vault, add secret variables to an environment, to globals and to a collection through the view models and
     save; read **every file** of the workspace (`environments`, `globals.yaml`, `requests/**/folder.yaml`, `.yaapic/`) and the vault file
     as bytes and assert none contains the distinctive secret values; the YAML rows have `secret: true` and `value: ""`; the vault file
     contains the entries as ciphertext.
  2. **Send**: a request using `{{apiKey}}` (secret environment variable) in a header reaches the echo with the real value; the history
     file (`.yaapic/history.jsonl`) contains the template `{{apiKey}}` and **not** the value.
  3. **Scripts**: `pm.environment.get("apiKey")` returns the value; `pm.environment.set("apiKey", "new-secret-9")` on the secret row keeps the
     flag, the vault holds the new value after the send and the YAML still blank; `console.log(pm.environment.get("apiKey"))` shows `••••`
     in the console, a failing `pm.test` whose message contains the secret shows `••••`, a script error containing it is redacted.
  4. **Lock**: after `Lock()` the secret variables are unresolved (the response panel shows the unresolved warning with the locked hint),
     the send still goes out, secret rows load empty, saving the environment while locked keeps the vault entries (unlock and the values
     are back), marking a new secret while locked is refused.
  5. **Unlock**: wrong passphrase -> false/`LastError`; right passphrase -> environments, globals and collection reload with values.
  6. **Restart**: a new shell on the same workspace with the same passphrase vault starts `Locked` and unlocks with the passphrase; a vault
     with an OS-style fake protector (auto-unlock) starts `Unlocked`.
  7. **Tamper and corruption**: flipping a byte of one entry loses only that entry with a `LoadWarnings` item; corrupt JSON leaves the
     file untouched and the app usable (secrets unavailable, error shown).
  8. **Move/delete**: deleting an environment removes its vault entries; moving a collection with secrets between top level and nested is refused;
     duplicating an environment copies its secrets.

## Out of scope

OS protectors (their cards test them), UI.

## Acceptance criteria

- [ ] The eight scenarios pass on Windows and Linux runners; 0 warnings; existing tests green.

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

`test: add secret vault end-to-end tests`
