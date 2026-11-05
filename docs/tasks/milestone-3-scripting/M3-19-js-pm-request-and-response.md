# M3-19: JavaScript `pm`: request and response

Status: todo
Platform: any
Depends on: M3-18, M3-09

## Goal

`pm.request` (read in both phases, mutate in pre-request) and `pm.response` (post-response only), Postman-compatible for
the commonly used members.

## Design decisions

- Read first: ADR 0009, M3-09 (`ScriptRequest`/`ScriptResponse` fields), M3-14 (`req.get`, `req.set`, `res.get`), M3-18
  (prelude layout, embedding rule), Postman's scripting reference for `pm.request` and `pm.response`.
- New prelude part `PmRequestResponse.js` (same embedding rule as M3-18). The request object keeps no cached copy: every
  getter calls `req.get`, every mutation calls `req.set` with the whole DTO right away; in post-response the host throws
  the read-only error, which the prelude lets propagate as a normal `Error`.
- `pm.request`:
  - `id`, `name`, `method` (get/set; set upper-cases), `url` (get returns a `Url` object, set accepts a string or a `Url`),
    `headers`, `body`.
  - `Url` object: `toString()` (the full URL text, variables untouched), `getHost()`, `getPath()`, `getPathWithQuery()`,
    `getQueryString()`, `protocol`, `host` (string array split on `.`), `port`, `path` (string array), `query` (a
    `PropertyList` of `{key,value}`) with `add({key,value})`, `upsert`, `remove(key)`, `get(key)`, `has(key)`, `toObject()`,
    `each(fn)`, `count()`; `update(string)`. Parsing is a small hand-written URL splitter (no `URL` class in QuickJS); a
    URL containing `{{variables}}` must survive untouched (split only on the first `?`, `#` and the first `://`).
  - `headers`: `PropertyList` with `get(name)` (case-insensitive), `has`, `add({key,value})`, `upsert({key,value})`,
    `remove(name)`, `each(fn)`, `count()`, `all()` (array of `{key,value}`), `toObject()`, `idx(i)`.
  - `body`: `mode` (get/set among `none|raw|urlencoded|formdata|binary`; setting `formdata` or `binary` throws
    `Error("pm.request.body mode 'formdata' cannot be set from a script")`), `raw` (get/set string), `urlencoded`
    (`PropertyList`, editable), `formdata` (read-only `PropertyList`), `toString()` (the raw text).
  - `pm.request.auth` and `pm.request.certificate`: unsupported stubs (as M3-18).
- `pm.response` (throws `Error("pm.response is only available in post-response scripts")` in pre-request):
  `code` (number), `status` (reason text), `responseTime` (ms), `responseSize` (bytes), `headers` (read-only `PropertyList`,
  duplicate header names kept, `get` returns the first), `text()`, `json(reviver?)` (a `JSON.parse` of `text()`; on failure
  throws `Error("Response body is not valid JSON: <parser message>")`), `reason()`, `size()` -> `{ body, header: 0, total }`,
  `cookies` is delivered by M3-22, `pm.response.to` by M3-21.
- `PropertyList` is one prelude class reused for headers, query, urlencoded and formdata, with a `readOnly` flag.

## Out of scope

Authentication objects, certificates, `pm.response.to.*` (M3-21), cookies and `sendRequest` (M3-22), file bodies.

## Acceptance criteria

- [ ] Tests with the real `ScriptHost` and `ScriptRequest`/`ScriptResponse`: method get/set; url get/set/`update`, query
      add/upsert/remove reflected in the **applied** definition (`ApplyTo`) including `{{variable}}` URLs and fragments;
      header add/upsert/remove/get case-insensitive; raw body set and mode switch; urlencoded edit; formdata read-only;
      every mutation in a post-response block throws the read-only message and leaves the request untouched; response
      `code/status/responseTime/responseSize/headers/text/json/reason/size` for JSON, text, empty and binary bodies; invalid
      JSON message; `pm.response` in pre-request throws.
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

`feat(scripting): add pm request and response to the JavaScript engine`
