# M3-20: JavaScript `pm.test` and `pm.expect`, core assertions

Status: done
Platform: any
Depends on: M3-19

## Goal

`pm.test` with synchronous, `done` and promise-returning bodies, and the first half of the Chai-style `pm.expect`
assertions with Chai-compatible failure messages.

## Design decisions

- Read first: ADR 0009, M3-14 (`test` op, `RunCollector`), M3-04 (`TestResult`), Chai's BDD `expect` documentation for
  message wording. New prelude part `PmTest.js` (same embedding rule as M3-18).
- `pm.test(name, fn?)`: records `{name, passed, message, ms}` through the `test` op **once**. No `fn` -> passed. `fn` that
  throws -> failed with the error message (`AssertionError: expected 409 to equal 204` style: the message is the
  `AssertionError.message`, other errors `TypeError: x is not a function`). `fn.length >= 1` -> called with `done`; a
  `done(err)` with an argument fails the test; a timeout is the block timeout. `fn` returning a promise -> recorded when it
  settles (runs through the engine job loop; a never-settling promise records nothing and is cut by the block timeout).
  `pm.test.skip(name, fn)` records nothing but a console `Info` "skipped <name>"; `pm.test.index()` returns the count so far.
  Duplicate names are allowed and recorded separately. A test name is converted with `String`.
- `AssertionError` class (name `AssertionError`, `message`, `actual`, `expected`, `showDiff`); `pm.expect.fail(message?)`.
- `pm.expect(value, message?)` returns an `Assertion` with Chai semantics: **language chains** that return `this`
  (`to, be, been, is, that, which, and, has, have, with, at, of, same, but, does, still, also, a`-as-chain is handled by
  the `a/an` assertion below), the flags `not`, `deep` (affects `equal`/`include`/`property` later), and these assertions
  (aliases in parentheses, both property and call forms where Chai allows):
  - `equal(v)` (`equals`, `eq`): strict `===` (NaN equals NaN is **false**, like Chai); `eql(v)` (`eqls`): deep equality
    (arrays, plain objects, Date, RegExp, NaN equals NaN); `deep.equal` = `eql`.
  - properties: `ok`, `true`, `false`, `null`, `undefined`, `NaN`, `exist` (not null/undefined), `empty` (string, array,
    object own enumerable keys, Map/Set size), `arguments` is **not** supported.
  - `a(type)` / `an(type)`: Chai type names via `Object.prototype.toString` (`string`, `number`, `boolean`, `array`,
    `object`, `null`, `undefined`, `function`, `date`, `regexp`, `error`, `map`, `set`), case-insensitive.
  - numbers: `above(n)` (`gt`, `greaterThan`), `least(n)` (`gte`), `below(n)` (`lt`, `lessThan`), `most(n)` (`lte`),
    `within(a, b)`, `closeTo(n, delta)` (`approximately`).
  - `length(n)` / `lengthOf(n)` (and `lengthOf` as a chain before `above/below/within`, e.g. `expect(a).to.have.lengthOf.above(2)`:
    support through a `lengthOf` flag).
- Messages follow Chai: `expected 409 to equal 204`, `expected 200 to not equal 200`, `expected [ 1, 2 ] to deeply equal [ 1, 3 ]`,
  `expected 'abc' to be a number`, `expected undefined to exist`, `expected 3 to be above 5`, with the value inspection a
  small `inspect()` (strings in single quotes, arrays/objects inline up to 80 chars then truncated with `...`, depth 2). A
  custom `message` argument prefixes the failure (`msg: expected ...`).
- Negation (`not`) flips every assertion above, with the `to not` message variants.

## Out of scope

`include`, `property`, `keys`, `members`, `match`, `string`, `oneOf`, `instanceOf`, `satisfy`, `throw` and
`pm.response.to.*` (M3-21), `pm.sendRequest` (M3-22).

## Acceptance criteria

- [x] Tests (real QuickJS, fake host recording tests): every assertion above passing and failing with the **exact** message
      text, with and without `not`, with a custom message; `pm.test` sync pass/fail, `done` pass/fail, promise resolve/
      reject, missing function, skip, never-settling promise cut by the timeout, ordering of results, error inside a
      test does not stop later tests, a non-test statement throwing outside `pm.test` fails the run (`Failed`) but keeps the
      tests recorded before it; `inspect()` truncation cases.
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

`feat(scripting): add pm.test and core pm.expect assertions`
