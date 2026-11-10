# M3-65: End-to-end tests: Lua scripting and mixed languages

Status: done
Platform: any
Depends on: M3-64, M3-32

## Goal

The Lua counterparts of the M3-64 scenarios and the mixed-language chain, over the loopback server.

## Design decisions

- Same file family: `Milestone3LuaEndToEndTests.cs` reusing `ScriptedShellFixture` and the loopback routes of M3-64 (no
  duplicated server code).
- Scenarios: Lua versions of pre-request mutation, token chaining, order, tests (same counts and **same failure messages** as the
  JavaScript scenario, asserted from the shared expectations), errors (pre-request error aborts, post-response error keeps the
  response, timeout), `pm.sendRequest` with `res, err`, variable scopes, cookies, persistence round trip and cancel.
- Mixed chain: collection JavaScript pre-request sets a variable, folder Lua pre-request reads it and adds a header, request
  JavaScript post-response test checks the response and the Lua-written variable, collection Lua post-response test checks the status:
  console order and test list match the specification of ADR 0009 (collection first), and the echo shows the header.
- Engine availability: with the Lua library hidden through the composition seam (simulate a missing library) a Lua block aborts the
  send with `Engine unavailable` and the shell lists the warning, while JavaScript requests still work.

## Out of scope

Vault (M3-66), UI.

## Acceptance criteria

- [x] All Lua scenarios, the mixed chain and the unavailable-engine case pass on Windows and Linux runners.
- [x] Existing tests untouched and green; 0 warnings; no leaked `yaapic-lua` threads.

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

`test: add Lua and mixed-language scripting end-to-end tests`
