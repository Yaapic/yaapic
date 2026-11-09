# M3-38: Core: secret-aware collection store and secret redaction

Status: done
Platform: any
Depends on: M3-37, M3-12

## Goal

Collection (top-level folder) variables use the vault too, and script output is scrubbed of secret values.

## Design decisions

- Read first: M3-37 (rules to mirror), `src/Yaapic.Core/Storage/ICollectionStore.cs`, `CollectionNode`, M3-12
  (`ScriptedRequestSender`), M3-04 (`ConsoleEntry`, `TestResult`, `ScriptError`).
- `SecretAwareCollectionStore(ICollectionStore inner, ISecretVault vault) : ICollectionStore`: `LoadTreeAsync` fills secret
  values of **top-level** folders only (nested folder variables are unused, ADR 0008; they are loaded unchanged);
  `SaveFolderAsync(folder, parentId)` applies the M3-37 save rules when `parentId == null` (owner `Collection` + the folder id),
  passes nested folders through untouched; `DeleteFolderAsync(id)` removes the owner entries when unlocked (check whether the
  id is a top-level folder from the tree; deleting a nested folder touches nothing); `MoveAsync` passes through (moving a
  folder to or from the top level changes whether its variables count: when a top-level folder with secret rows becomes nested
  or the reverse, throw `StorageException("Cannot move a collection that has secret variables")`; test it). Other members pass
  through.
- `public interface ISecretRedactor { string Redact(string text); }` and `SecretRedactor(ISecretVault vault)`: replaces every
  value of `vault.GetSecretValues()` (longest first, ordinal, so a longer secret containing a shorter one is replaced whole)
  with `••••`; null/empty text unchanged; allocation-free fast path when there are no secrets. `ScriptRedaction` static helpers
  `Redact(ConsoleEntry)`, `Redact(TestResult)` (name and message), `Redact(ScriptError)`, `Redact(ScriptRunResult)`.
- `ScriptedRequestSender` gets an optional constructor parameter `ISecretRedactor? redactor = null` and applies it to every
  `ScriptRunResult` it returns (console, tests, error) and to the `NetworkLog` **after** the run (so scripts still see real
  values). The abort and normal paths both redact. Existing tests (no redactor) are unchanged.
- The redactor also covers values that appear **encoded** only as written: no base64/url-encoding detection (document the limit
  in XML docs and ADR 0010 accepted limits if not present).

## Out of scope

UI, key/value masking (M3-46), history redaction (history stores templates).

## Acceptance criteria

- [x] Tests: collection store load/save/delete/move rules like M3-37; redactor cases (no secrets, one, several, overlapping,
      repeated, case-sensitive, shorter than 4 ignored, locked vault no-op); `ScriptedRequestSender` with a fake engine that logs
      a secret, fails a test with a secret in the message and throws an error containing it: all outputs redacted while the
      variable value used by the transport is the real one.
- [x] 0 warnings, XML docs, all tests green.

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

`feat(core): vault-backed collection variables and secret redaction`
