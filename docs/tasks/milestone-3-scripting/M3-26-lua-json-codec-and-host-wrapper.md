# M3-26: Lua prelude: JSON codec and typed host wrapper

Status: todo
Platform: any
Depends on: M3-25, M3-14

## Goal

A pure-Lua JSON codec and the wrapper every later Lua `pm` part uses to call `__host` with Lua tables.

## Design decisions

- Read first: M3-14 (JSON op protocol), M3-25 (prelude embedding: `const string` per part, evaluated as its own chunk,
  `LuaPrelude.Build()` concatenation order), ADR 0009 (Lua idioms: `pm.null`, arrays).
- Part `PmJson.lua` defines a local-to-prelude (not user visible except `pm.json`) codec:
  - `json.encode(value)` -> string: tables with only positive integer keys `1..n` (or an explicit `json.array()` marker table
    for empty arrays) become arrays, other tables objects (string keys; integer keys converted to strings; mixed tables are an
    error `"cannot encode mixed table"`), `pm.null` -> `null`, strings escaped (`\"`, `\\`, control chars as `\u00XX`, valid
    UTF-8 passed through), numbers: integers as integers, floats shortest round-trip (`%.17g` trimmed), `nan`/`inf` -> error,
    functions/userdata -> error `"cannot encode <type>"`, depth limit 100.
  - `json.decode(text)` -> value: objects -> tables, arrays -> tables with `n` field set to the length (so trailing `null`s
    are visible), `null` -> `pm.null`, numbers -> integer when the literal has no fraction/exponent and fits int64 else float,
    full string escapes including `\uXXXX` and surrogate pairs to UTF-8, errors raised as
    `error("invalid JSON at position N: <reason>", 0)`, depth limit 100, trailing garbage rejected.
  - `pm.null` is a unique frozen sentinel table with `__tostring` = `"null"`; `pm.json = {encode=..., decode=...}` is public
    (scripts may use it; it is also the Lua answer to JavaScript's `JSON`).
- `host(op, tbl)` wrapper: encodes `tbl`, calls `__host(op, json)`, decodes the reply, returns `ok` or raises
  `error(msg, 0)` for `err`; arrays returned by the host come back as tables with `n`.
- Performance: decoding a 1 MB JSON document must take under 1 s in the test (QuickJS is not the reference here, just a
  budget to avoid quadratic string handling: build with table buffers, `string.byte`/`string.find` scanning).

## Out of scope

Any `pm` member, a streaming parser, comments or trailing commas in JSON.

## Acceptance criteria

- [ ] Tests (real Lua through `LuaEngine` with a fake host): the JSON conformance cases (RFC 8259 valid and invalid samples:
      `[]`, `{}`, nested, unicode escapes, surrogate pairs, big ints, floats, `1e3`, `-0`, leading zeros rejected, trailing
      comma rejected, unterminated string, depth 101) for decode; encode/decode round trip of nested data with `pm.null`;
      arrays with trailing nulls keep `n`; the 1 MB budget; host wrapper raises on `err` and returns decoded data on `ok`.
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

`feat(scripting): add the Lua JSON codec and host wrapper`
