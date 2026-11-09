# M3-33: Domain and YAML: secret flag on variable rows

Status: done
Platform: any
Depends on: M3-02

## Goal

A variable row can be marked secret, and the environment, folder and globals files record the flag (ADR 0010). The value
handling (vault) comes later; this card only carries the flag through the model and the files.

## Design decisions

- Read first: `docs/adr/0010-secret-vault.md`, `src/Yaapic.Domain/Http/KeyValueEntry.cs`,
  `src/Yaapic.Infrastructure/Storage/{RequestFileDto,EnvironmentYamlSerializer,FolderYamlSerializer,GlobalsYamlSerializer}.cs`.
- `KeyValueEntry` becomes `KeyValueEntry(string Key, string Value, bool Enabled = true, bool IsSecret = false)`. Existing call
  sites keep compiling (default value). Equality includes the flag (records).
- `KeyValueDto` gains `bool? Secret`. **Variable lists only** (environment `variables`, folder `variables`, globals
  `variables`) read and write it; request `params` and `headers` never write `secret` and ignore it on read (the mapper that
  converts DTO <-> entries takes a `bool allowSecret` argument; one shared mapper, no copy).
- Write rule (documented exception to "every key always written"): `secret: true` is written **only** for secret rows, after
  `enabled`; non-secret rows are byte-identical to milestone 2 output, so existing workspaces produce no diff. Read: missing or
  `false` -> not secret.
- No value handling here: a secret row is read and written like any row (the decorators of M3-37/M3-38 blank and fill the
  value). `docs/file-format.md` already documents `secret: true` (M3-02): fix any difference.

## Out of scope

The vault, decorators, UI, masking.

## Acceptance criteria

- [x] Tests: environment, folder and globals round trips with a mix of secret and normal rows; non-secret output unchanged
      (golden string equal to the milestone 2 output); `secret: true` ignored and never written for params/headers; milestone 2
      files load with `IsSecret == false`; `KeyValueEntry` equality with the flag; key order (`key, value, enabled, secret`).
- [x] Existing tests untouched and green, 0 warnings, XML docs updated.

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

`feat: carry a secret flag on variable rows and store it in YAML`
