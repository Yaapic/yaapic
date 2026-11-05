# M3-28: Lua `pm`: request and response

Status: todo
Platform: any
Depends on: M3-27, M3-09

## Goal

The Lua twin of M3-19.

## Design decisions

- Read first: M3-19 (members to mirror, flush-per-mutation rule), M3-26, M3-27.
- Prelude part `PmRequestResponse.lua`. Closures, dot calls, camelCase and snake_case aliases as in M3-27.
- `pm.request`: `id`, `name`, `method` (use `pm.request.get_method()` / `set_method(m)`; plain field assignment is **not**
  supported in Lua because the table is read-only: list this in the compatibility doc), `url` object with the same members as
  JavaScript (`to_string`, `get_host`, `get_path`, `get_path_with_query`, `get_query_string`, `protocol`, `host`, `port`,
  `path`, `query` list API, `update(str)`), `pm.request.set_url(string)`, `headers` list (`get`, `has`, `add{key=,value=}`,
  `upsert{...}`, `remove(name)`, `each(fn)`, `count()`, `all()`, `to_object()`), `body` (`mode`, `raw`, `urlencoded` list,
  `formdata` read-only; `get_mode/set_mode`, `get_raw/set_raw`). The URL splitter is a Lua port of the JavaScript one (same
  test table).
- `pm.response` (post-response only, same error text): `code`, `status`, `response_time`, `response_size`, `headers`
  (read-only list), `text()`, `json()` (decoded with `pm.json.decode`; invalid JSON raises
  `"Response body is not valid JSON: <reason>"`), `reason()`, `size()` -> `{body=, header=0, total=}`.
- The list class is one prelude class reused for headers, query, urlencoded, formdata (`read_only` flag), 1-based `idx(i)`.

## Out of scope

Authentication objects, `pm.response.to` (M3-30), cookies and `sendRequest` (M3-31).

## Acceptance criteria

- [ ] Tests: the M3-19 table translated to Lua (method get/set, url get/set/update, query add/upsert/remove with
      `{{variable}}` URLs and fragments, headers, raw body, urlencoded, formdata read-only, read-only failures in
      post-response, response members for JSON/text/empty/binary bodies, invalid JSON message, pre-request access error) using
      the real `ScriptRequest.ApplyTo` for assertions.
- [ ] 0 warnings, all tests green, AOT smoke `OK`.

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

`feat(scripting): add pm request and response to the Lua engine`
