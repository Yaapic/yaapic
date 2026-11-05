# M3-05: YAML request serializer: scripts

Status: todo
Platform: any
Depends on: M3-03

## Goal

Request files read and write the `scripts` section (ADR 0009, `docs/file-format.md`).

## Design decisions

- Read first: `src/Yaapic.Infrastructure/Storage/RequestYamlSerializer.cs`, `RequestFileDto.cs` (how `SettingsDto` is
  mapped), `docs/file-format.md`, `tests/Yaapic.Infrastructure.Tests/Storage/`.
- `RequestFileDto` gains `public ScriptsDto? Scripts { get; set; }` after `Settings`. New `[YamlObject(NamingConvention.LowerCamelCase)]`
  internal partial classes in `RequestFileDto.cs` (or a new `ScriptsDto.cs` shared with the folder serializer, M3-06):
  `ScriptsDto { ScriptBlockDto? PreRequest; ScriptBlockDto? PostResponse; }` and `ScriptBlockDto { string? Language; string? Source; }`.
- Write: always write `scripts` with both blocks; `language` via `ScriptLanguage.ToText()`; `source` as a literal block
  scalar when it contains a newline (the existing multi-line rule applies; check that VYaml emits `|` for it and that a
  trailing newline round-trips). Key order: `... settings, scripts`; inside `scripts`: `preRequest, postResponse`; inside a
  block: `language, source`.
- Read: a missing `scripts`, block, `language` or `source` means `ScriptBlock.Empty` values; an unknown `language` is a
  `StorageException`-style format error with the same type and message style the serializer already uses for an unknown
  `method` ("Unknown script language 'x'"). Version stays 1.
- Round trip must preserve: tabs, `\r\n` inside the source (normalised to `\n` on write like other strings, record that
  in the test), unicode, a source starting with spaces, `#` and `:` characters, and a very long source (100 KB).

## Out of scope

Folder files (M3-06), any UI.

## Acceptance criteria

- [ ] Tests: default definition writes the exact YAML block for empty scripts; full round trip for JS and Lua blocks with
      multi-line sources and the special characters above; files without `scripts` (milestone 2 files, the fixtures under
      `tools/screenshots/workspace`) still load with `ScriptSet.Empty`; unknown language fails with the message; key order.
- [ ] `FileRequestStore` round trip with scripts on disk (temp workspace).
- [ ] 0 warnings with the AOT analyzers (the serializer is a library: no publish needed), all tests green;
      `docs/file-format.md` already describes the shape (M3-01): fix any difference.

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

`feat(infrastructure): store request scripts in YAML`
