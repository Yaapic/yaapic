# Scripting compatibility with Postman (JavaScript)

Yaapic runs Postman-style scripts on QuickJS-NG with a `pm` object (ADR [0009](adr/0009-scripting.md)). This page lists
what is supported and what differs. It is kept in step with the conformance suite
`tests/Yaapic.Scripting.Tests/Conformance/JavaScriptConformanceTests.cs`: every `supported` row is covered by a case there,
and every `unsupported` row by an unsupported-member case.

## The `pm` surface

| Member | Status | Note |
|---|---|---|
| `pm.test`, `pm.expect` | supported | Chai-style chains: `property`, `include`, `an`, `lengthOf`, `match`, `oneOf`, `eql`, `below`, `true`/`false`, `ok`... A failed `pm.test` is a test result with the assertion message, not a script error. |
| `pm.response.code`, `status`, `responseTime`, `text()`, `json()`, `headers` | supported | `headers.get` is case-insensitive. `json()` throws on a non-JSON body, as in Postman. |
| `pm.response.to.have.status`, `header`, `jsonBody`; `to.be.ok` | supported | |
| `pm.response.to.have.jsonSchema` | unsupported | Fails with `... is not supported in Yaapic`. |
| `pm.request.method`, `url`, `headers`, `body` | supported | Read in both phases, mutable in pre-request (`headers.add/upsert/remove`, `url.query.add`, `body.raw`). |
| `pm.request.auth` | unsupported | Use a header in a pre-request script. |
| `pm.variables`, `pm.environment`, `pm.collectionVariables`, `pm.globals` | supported | `get`, `set`, `unset`, `has`, `clear`, `toObject`; `replaceIn` on `pm.variables` (dynamic variables such as `{{$guid}}`, `{{$timestamp}}`, `{{$randomInt}}` work). |
| `pm.environment.name` | unsupported | |
| `pm.info` | partial | `eventName`, `requestName`, `requestId`; `iteration` is always `0`. |
| `pm.sendRequest` | supported | String and options forms; callback and promise (`await`) styles; raw and urlencoded bodies. Limited to 20 calls per run. |
| `pm.cookies` | partial | Read-only view of the `Set-Cookie` headers of the current response. `pm.cookies.jar()` throws `The cookie jar is not available in Yaapic yet`. |
| `console.*` | supported | Objects are printed as JSON. |
| `postman.setEnvironmentVariable`, `getEnvironmentVariable`, `clearEnvironmentVariable`, and the global equivalents | supported | Aliases of the `pm` calls. |
| `postman.setNextRequest`, `pm.execution` | unsupported | Needs the Runner (milestone 6). |
| `pm.iterationData` | unsupported | Needs the Runner (milestone 6). |
| `pm.vault` | unsupported | |
| `pm.visualizer` | unsupported | |
| `pm.require` | unsupported | |
| `require('crypto-js')`, `CryptoJS` | unsupported | `require` and `CryptoJS` exist as stubs that throw `... is not supported in Yaapic`. |

Unsupported members throw an `Error` whose message is `<member> is not supported in Yaapic`, so a collection imported from
Postman never fails silently.

The language itself is QuickJS: ES2023 syntax (template literals, destructuring, classes with private fields, optional
chaining, `Array.prototype.flat`, `Object.entries`, `async`/`await` at the top level, `toFixed`) works. There is no `Intl`,
no timers (`setTimeout`) and no host module.

## Differences from Postman

- **Layered write fallback.** `pm.environment.set`, `pm.collectionVariables.set` and `pm.globals.set` write to their scope
  and are persisted after the send. When the scope does not exist (no active environment, or a request outside a
  collection), the value lives for that send only and a `warn` line is written to the console.
- **No bundled libraries.** `CryptoJS`, `lodash`, `moment`, `cheerio`, `tv4`, `uuid` and `atob`/`btoa` helpers are not
  provided; use plain JavaScript (for example a hand-rolled checksum) or `{{$guid}}` for identifiers.
- **Cookies.** `max-age` is ignored when reading `Set-Cookie` headers; there is no cookie jar.
- **Scripts see templates.** In pre-request, `pm.request.url` and the headers keep their `{{variables}}`; call
  `pm.variables.replaceIn` to resolve a string. The sender resolves the mutated request afterwards.
- **Errors.** A pre-request error or timeout (5 s per block) aborts the send; a post-response error keeps the response and
  the tests already recorded.

## Lua differences (request and response)

- **Setters instead of assignment.** The `pm.request`, `pm.request.url`, `pm.request.body` and `pm.response` tables are read-only,
  so `pm.request.method = 'POST'` raises an error. Use `pm.request.set_method(m)`, `pm.request.set_url(s)`,
  `pm.request.url.update(s)`, `pm.request.body.set_mode(m)` and `pm.request.body.set_raw(s)`. Reading `pm.request.method`
  and `pm.request.body.mode` / `raw` still works, as do the `get_*` functions.
- **No URL part setters.** `pm.request.url.protocol`, `host`, `port`, `path` and `hash` are read-only (arrays are 1-based tables);
  rebuild the URL with `update`.
- **Lists.** `headers`, `url.query`, `body.urlencoded`, `body.formdata` and `pm.response.headers` share one list API
  (`get`, `has`, `add{...}`, `upsert{...}`, `remove`, `each`, `count`, `all`, `idx` (1-based), `to_object`); every function is
  called with a dot, and both `snake_case` and `camelCase` names exist.
- **`pm.response` in pre-request.** Fetching `pm.response` itself does not fail; the first member access raises
  `pm.response is only available in post-response scripts`.

## Lua differences (`pm.test` and `pm.expect`)

- **Every assertion is a call.** `pm.expect(x).to.equal(1)`, `pm.expect(x).to.be.ok()`. Chai property assertions become
  zero-argument calls: `ok()`, `is_true()`, `is_false()`, `is_null()` (matches `pm.null`), `is_nil()` (matches `nil`),
  `is_nan()`, `exist()`, `empty()`.
- **Reserved words.** `not` is `is_not` and `and` is `also` (`pm.expect(x).to.is_not.equal(1)`, `.above(1).also.below(5)`);
  `deep` is a property (`deep.equal`). `a(type)` accepts `string`, `number`, `integer`, `float`, `boolean`, `table`, `array`,
  `object`, `function`, `nil` and `null`.
- **Names.** `close_to` (`closeTo`), `length` (`length_of`, `lengthOf`), `gt`, `gte`, `lt`, `lte`; there is no `lengthOf.above(n)`
  chain.
- **Equality.** `equal` is `==` (tables by identity); `eql` is deep (arrays by length, objects by keys, `NaN` never equals `NaN`).
- **Failure messages are the Chai texts** (`expected 409 to equal 204`), with values rendered like JavaScript (`nil` is
  `undefined`, `pm.null` is `null`, `{ a = 1 }` is `{ a: 1 }`), so a failure reads the same in both languages.
- **`pm.test`.** `pm.test(name, fn)` runs `fn` under `pcall`; there is no `done` callback and no promise support. A failed test's
  message is `AssertionError: ...`, or the error text without its `origin:line:` prefix (the console keeps the position).
  Test durations are recorded as 0 ms.

## Reporting a gap

Open an issue with the script (or the smallest piece that fails), the Postman behavior you expect, and what Yaapic
printed in the console. A fix starts as a new case in the conformance suite.
