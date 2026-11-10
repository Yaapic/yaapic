# M3-29: Lua `pm.test` and `pm.expect`, core assertions

Status: done
Platform: any
Depends on: M3-28

## Goal

The Lua twin of M3-20: `pm.test` and the first half of the `expect` assertions, with Lua-friendly names.

## Design decisions

- Read first: M3-20 (assertion list and Chai message texts to reuse **verbatim**, so a failure message reads the same in both
  languages), M3-26 (`pm.json`, `pm.null`), ADR 0009 (Lua idioms).
- Lua restrictions that shape the API: a bare `a.b.c` is not a Lua statement, so **every assertion is a call**; `not`, `and`,
  `nil`, `true`, `false`, `in` are reserved words and cannot follow a dot. Naming rules (final):
  - chain words are properties returning the same assertion: `to`, `be`, `been`, `is`, `that`, `which`, `has`, `have`,
    `with`, `at`, `of`, `same`, `but`, `does`, `still`, `also`; `and` is replaced by `also`; negation is the property
    `is_not` (`pm.expect(x).to.is_not.equal(1)`; `pm.expect(x).to.be.is_not.ok()`), `deep` is a property.
  - value assertions are calls: `equal(v)` (`equals`, `eq`), `eql(v)` (`eqls`), `above(n)` (`gt`), `least(n)` (`gte`),
    `below(n)` (`lt`), `most(n)` (`lte`), `within(a, b)`, `close_to(n, d)`, `length(n)` (`length_of`), `a(type)` (`an`).
  - "property" assertions of Chai become zero-argument calls: `ok()`, `is_true()`, `is_false()`, `is_null()` (matches
    `pm.null`), `is_nil()` (matches `nil`), `is_nan()`, `exist()`, `empty()`.
  - `a(type)` type names: `string`, `number`, `integer`, `float`, `boolean`, `table`, `array`, `object`, `function`, `nil`,
    `null`; `array` = table whose keys are `1..n`, `object` = any other table (not the null sentinel).
  - Equality: `equal` is `==` on scalars (tables by identity); `eql` is deep equality over tables (arrays by `n`/length,
    objects by keys; `pm.null == pm.null`; NaN not equal NaN).
- `pm.test(name, fn)`: runs `fn` under `pcall`; failure message is the assertion message (an `AssertionError` is a table with
  `name = "AssertionError"`, `message`, `actual`, `expected`, `__tostring`; any other error value is converted with
  `tostring`, stripping the `chunk:line:` prefix kept separately in the console line). `fn == nil` -> passed. `pm.test.skip`,
  `pm.test.index()`. No `done`/promise support in Lua (document). `pm.expect.fail(message)`.
- Messages follow M3-20 exactly (`expected 409 to equal 204`), values rendered with a Lua `inspect()` that prints strings
  in single quotes, tables like JavaScript objects/arrays (`{ a: 1 }`, `[ 1, 2 ]` for arrays), `pm.null` as `null`, `nil` as
  `undefined` (so messages are identical across languages; test it).

## Out of scope

`include`, `property`, `keys`, `members`, `match`, `string`, `one_of`, `instance_of`, `satisfy`, `throw`, response helpers
(M3-30).

## Acceptance criteria

- [x] Tests (real Lua, recording fake host): each assertion passing/failing/negated with the **same message text** as the
      JavaScript tests of M3-20 (share a table of `(expression, expected message)` pairs between both test classes via a small
      data file `tests/Yaapic.Scripting.Tests/Conformance/ExpectMessages.json` read at test time — JSON through
      `System.Text.Json` source generators); `pm.test` pass/fail/skip/ordering/errors inside tests, runtime errors vs
      assertion errors, results recorded through the `test` op; `inspect()` cases.
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

`feat(scripting): add pm.test and core pm.expect assertions to the Lua engine`
