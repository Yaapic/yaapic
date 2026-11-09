# M3-32: Lua conformance suite and compatibility doc

Status: done
Platform: any
Depends on: M3-31, M3-23

## Goal

The Lua equivalent of M3-23: end-to-end Lua scripts through `ScriptedRequestSender`, plus the Lua part of the compatibility
document.

## Design decisions

- `tests/Yaapic.Scripting.Tests/Conformance/LuaConformanceTests.cs`, same shape as `JavaScriptConformanceTests` (reuse its
  case record and runner helper; move them to a shared `ConformanceRunner` if needed, no copy).
- At least 30 cases mirroring the JavaScript idioms in Lua: status/time/header tests, JSON value tests through `pm.response.json()`
  tables (including `pm.null`, arrays with `n`, nested tables), token saved to an environment variable and used in the next
  request, timestamp/`replace_in` with `{{$guid}}`, header signature in pre-request using `string.format` and a hand-rolled
  checksum, counters in `pm.collection_variables`, globals set/unset, expect chains, response helpers, `pcall` around `json()`,
  loops (`for`, `ipairs`, `pairs`, `table.sort`), `string` methods and patterns, integer/float behaviour, `utf8` functions,
  closures/varargs, `pm.sendRequest` with `res, err`, `console.log` of tables, `pm.test` failure messages equal to the
  JavaScript ones, a runtime error in post-response keeps the response, an infinite loop times out, a pre-request error
  aborts the send.
- Unsupported table (>= 8): `pm.execution`, `pm.iterationData`, `pm.vault`, `pm.request.auth`, `pm.response.to.have.json_schema`,
  `pm.cookies.jar()`, `require("x")`, `os.execute`, `io.open`, `instance_of`.
- Mixed-language chain test: collection JS pre-request sets a variable, request Lua pre-request reads it and adds a header,
  folder Lua post-response test reads the response, request JS post-response test sees the variable written by the Lua one.
- `docs/scripting-compatibility.md` (from M3-23): add a Lua section (naming rules, `is_not`, call-style assertions, Lua
  patterns instead of regexes, 1-based tables vs 0-based JSON paths, synchronous `pm.sendRequest`, no `done`/promises, no
  `postman.*`), and a table of JavaScript member -> Lua member.

## Out of scope

UI, vault, E2E over the loopback server (M3-64, M3-65).

## Acceptance criteria

- [x] At least 30 supported + 10 unsupported Lua cases and the mixed-language chain test pass.
- [x] Compatibility doc updated and matching the tests.
- [x] 0 warnings, all tests green.

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

`test(scripting): add the Lua conformance suite and extend the compatibility doc`
