# M3-30: Lua `pm.expect` collections/strings and `pm.response.to`

Status: done
Platform: any
Depends on: M3-29

## Goal

The Lua twin of M3-21.

## Design decisions

- Read first: M3-21 (semantics and messages to reuse verbatim), M3-29 (naming rules, shared message table).
- Calls added to the Lua `Assertion`: `include(v)` (`contain`; string substring, array member, object subset, deep with
  the `deep` property), `property(name[, value])` (`nested.property(path)`, `own_property(name)`; the subject becomes the
  property value so `pm.expect(t).to.have.property("a").that.equal(1)` works), `keys(...)`, `members(list)` (`include.members`,
  `deep.members`, `ordered.members`), `match(pattern)` where `pattern` is a **Lua pattern** (documented difference from
  JavaScript regexes; `pm.regex` is not provided), `string(sub)`, `one_of(list)`, `satisfy(fn)`, `throw([message])` (calls the
  function under `pcall`; with a string checks that the error message contains it). `instance_of` is **not** supported
  (`"pm.expect(...).instance_of is not supported in Lua"`).
- `pm.response.to` (post-response only) with the same helpers as JavaScript, call-style: `have.status(codeOrText)`,
  `have.header(name[, value])`, `have.body(textOrPattern)`, `have.json_body([path[, value]])`, `have.json_schema` unsupported,
  and the state flags as zero-argument calls: `be.ok()`, `be.success()`, `be.info()`, `be.redirection()`, `be.client_error()`,
  `be.server_error()`, `be.error()`, `be.accepted()`, `be.bad_request()`, `be.unauthorized()`, `be.forbidden()`,
  `be.not_found()`, `be.rate_limited()`, `be.with_body()`, `be.json()`, `be.html()`, `be.text()`; negation with `is_not`;
  camelCase aliases for the multi-word names (`jsonBody`, `clientError`, ...).
- Failure messages identical to JavaScript (M3-21), through the shared table.

## Out of scope

JSON schema, regex libraries, `instance_of`.

## Acceptance criteria

- [x] Tests (real Lua): the M3-21 cases translated, messages compared through the shared `ExpectMessages.json` where the
      expression exists in both languages; Lua-pattern `match`; `throw` forms; `instance_of` message; every `pm.response.to`
      helper over the same response matrix as M3-21; `have.json_body` paths (`data[1].id` is **1-based in Lua** — decide and
      document: paths use JavaScript-style 0-based indices **as written by the user for JSON** (`data[0].id`) because they
      address the JSON document, not a Lua table; test both).
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

`feat(scripting): add remaining pm.expect assertions and response helpers to Lua`
