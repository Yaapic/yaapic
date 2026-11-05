# M3-06: YAML folder serializer: scripts

Status: done
Platform: any
Depends on: M3-05

## Goal

`folder.yaml` reads and writes the same `scripts` section, so a collection or any nested folder can own scripts.

## Design decisions

- Read first: `src/Yaapic.Infrastructure/Storage/FolderYamlSerializer.cs`, `FolderFileDto.cs`, the DTOs added by M3-05.
- `FolderFileDto` gains `ScriptsDto? Scripts` after `Variables`; reuse `ScriptsDto` and the mapping helpers of M3-05 (move
  them into a small `internal static class ScriptsMapper` used by both serializers if they are not shared yet; no
  duplicated code).
- Key order for a folder: `version, id, name, description, variables, scripts`. Same read/write rules as M3-05.
- Scripts exist on **every** folder level (collection and nested folders), unlike variables which are only used on the
  top-level folder (ADR 0008).
- `FileCollectionStore`: saving a folder keeps its scripts; renaming or moving a folder keeps them (they live in
  `folder.yaml`, which is moved with the directory); `LoadTreeAsync` returns `FolderDefinition.Scripts` filled. Check
  that the workspace index scanner (M2-28) does not drop the new key.

## Out of scope

UI, running the scripts.

## Acceptance criteria

- [x] Tests: folder round trip with scripts (JS and Lua); a milestone 2 `folder.yaml` loads with `ScriptSet.Empty`; key
      order; `FileCollectionStore` tests: save, rename, move keep scripts; nested folder scripts load in the tree.
- [x] 0 warnings, all tests green, `docs/file-format.md` matches.

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

`feat(infrastructure): store folder scripts in YAML`
