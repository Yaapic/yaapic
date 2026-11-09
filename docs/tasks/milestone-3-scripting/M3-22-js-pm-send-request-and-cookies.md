# M3-22: JavaScript `pm.sendRequest` and `pm.cookies`

Status: done
Platform: any
Depends on: M3-21, M3-13

## Goal

Extra HTTP calls from a script (callback and promise forms) and read-only cookies of the current response.

## Design decisions

- Read first: ADR 0009 (cookies paragraph), M3-13 (`ScriptSendService`, limits), M3-09 (`SetCookieParser`), M3-14 (`send`,
  `cookies` ops), Postman's reference for `pm.sendRequest`.
- New prelude part `PmSend.js` (same embedding rule as M3-18).
- `pm.sendRequest(input, callback?)`:
  - `input`: a URL string (GET) or an options object `{ url, method, header, body }` where `header` is an array of
    `{key,value}` **or** a plain object, and `body` is `{ mode: 'raw'|'urlencoded', raw, urlencoded: [{key,value}] , options }`
    (an unknown `mode` throws `Error("pm.sendRequest body mode 'x' is not supported")`). `url` may be a string or a `Url`
    object (M3-19). `{{variables}}` are resolved by the host (M3-13).
  - Execution: build the `RequestDto`, call the blocking `send` op **at call time**, then deliver the result asynchronously:
    with a callback, `Promise.resolve().then(() => callback(err, res))` (so code after the call runs first, as in Postman);
    without a callback return a `Promise` resolving to `res` or rejecting with an `Error`. `err` is `null` on success, an
    `Error` with the host message on failure, `res` is `undefined` on failure.
  - `res` has the `pm.response` read API of M3-19 (`code`, `status`, `headers`, `text()`, `json()`, `responseTime`,
    `responseSize`, `reason()`, `size()`, `cookies`) and `to` from M3-21 (build both from one `Response` class parametrised by
    the DTO, do not duplicate).
  - A callback that throws must fail the run: catch inside the delivery wrapper, store the error in `__yaapic.errors`, and
    have the engine (M3-17: add a small read of `__yaapic.errors` after `RunPendingJobs`) turn the first one into a
    `Failed` result with that error. Test it.
  - The send limit and failures come from the host (`Error` text unchanged).
- `pm.cookies`: `get(name)` -> value string or `undefined`, `has(name)`, `toObject()` -> `{name: value}`, `each(fn)`,
  `count()`, `all()` -> array of `{name,value,domain,path,expires,httpOnly,secure}`; data from `cookies` op (empty in
  pre-request). `pm.cookies.jar()` throws `Error("The cookie jar is not available in Yaapic yet")`. `res.cookies` (sendRequest
  response) offers the same read API over that response's `Set-Cookie` headers.

## Out of scope

Cookie jar, proxy options, certificates, streaming bodies, `pm.sendRequest` with `formdata` or file bodies.

## Acceptance criteria

- [x] Tests with a recording fake host and with the real `ScriptedRequestSender` + `FakeHttpTransport`: string form, options
      form (header array and object, raw JSON body, urlencoded body), callback order (code after the call runs before the
      callback), promise form with `await`, error form for a failed send and for the 21st call, a throwing callback fails the
      run, variables inside the URL and body are resolved, `res.json()`/`res.to.have.status`; `pm.cookies` with two
      `Set-Cookie` headers, in pre-request (empty), `jar()` message; unsupported body mode message.
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

`feat(scripting): add pm.sendRequest and pm.cookies to the JavaScript engine`
