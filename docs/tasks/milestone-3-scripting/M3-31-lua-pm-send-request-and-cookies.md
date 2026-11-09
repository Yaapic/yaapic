# M3-31: Lua `pm.sendRequest` and `pm.cookies`

Status: done
Platform: any
Depends on: M3-30, M3-13

## Goal

The Lua twin of M3-22, synchronous.

## Design decisions

- Read first: M3-22, M3-13, ADR 0009 (Lua idioms).
- `pm.sendRequest(input[, callback])` (alias `pm.send_request`): `input` is a URL string or a table
  `{ url, method, header, body }` with the same shapes as JavaScript (`header` array of `{key=,value=}` or a map, `body`
  `{ mode = "raw"|"urlencoded", raw = , urlencoded = {...} }`). It calls the blocking `send` op and **returns `res, err`**:
  `res` is a response table with the `pm.response` read API (`code`, `status`, `headers`, `text()`, `json()`,
  `response_time`, `response_size`, `reason()`, `size()`, `cookies`, `to`), `err` a string; exactly one of them is `nil`.
  With a callback it is also called **after the call returns to the script's current statement boundary** is impossible in
  Lua, so the callback form is simply called immediately, before `sendRequest` returns, as `callback(err, res)` with the same
  argument order as JavaScript; its return value is ignored; the call still returns `res, err`. Document both forms.
- A callback that raises propagates as a normal script error (no deferred delivery in Lua).
- `pm.cookies` read API as JavaScript, snake_case with aliases: `get`, `has`, `to_object`, `each`, `count`, `all`, `jar()` raising
  `"The cookie jar is not available in Yaapic yet"`; `res.cookies` likewise.
- Limits and error texts come from the host, unchanged (21st call error).

## Out of scope

Coroutines, cookie jar, non-raw bodies.

## Acceptance criteria

- [x] Tests (fake host and the real `ScriptedRequestSender` + `FakeHttpTransport`): string and table forms, header array and
      map, raw and urlencoded bodies, `res, err` for success and failure, callback form order and error propagation, variables
      resolved in URL/body, 21st call error, `res.json()` and `res.to.have.status`, cookies in pre-request (empty) and post-
      response, `jar()` message, unsupported mode message.
- [x] 0 warnings, all tests green, AOT smoke `OK`.

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

`feat(scripting): add pm.sendRequest and pm.cookies to the Lua engine`
