# Scripting compatibility with Postman (JavaScript)

Yaapic runs Postman-style scripts on QuickJS-NG with a `pm` object (ADR [0009](adr/0009-scripting.md)). This page lists
what is supported and what differs. It is kept in step with the conformance suite
`tests/Yaapic.Scripting.Tests/Conformance/JavaScriptConformanceTests.cs` (and, for Lua, `LuaConformanceTests.cs`): every
`supported` row is covered by a case there, and every `unsupported` row by an unsupported-member case.

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
| `pm.info` | partial | `eventName`, `requestName`, `requestId`; `iteration` is always `0` and `iterationCount` `1`. |
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
no timers (`setTimeout`), no `BigInt`, `WeakRef` or `DOMException`, and no host module (`std`, `os` and `require` are
undefined or throw as above).

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

## Lua overview

Scripts written in Lua 5.4 use the same `pm` object with Lua naming; the Lua differences below are checked by
`LuaConformanceTests.cs`, which also runs a mixed-language chain (JavaScript collection script, Lua folder and request
scripts, JavaScript request test) and shows that variables and the response are shared across languages.

- **Naming.** Functions are `snake_case` (`replace_in`, `to_string`, `response_time`, `json_body`); the JavaScript camelCase names
  also exist for most of them (`replaceIn`, `jsonBody`).
- **Reserved words.** Chai's `not` and `and` are `is_not` and `also`.
- **Call-style assertions.** Property assertions are calls: `pm.response.to.be.ok()`, `pm.expect(x).to.is_null()`.
- **Patterns, not regexes.** `match(pattern)` and `have.body(pattern)` take Lua patterns (`%d+`, not `\d+`).
- **1-based tables, 0-based JSON paths.** `pm.response.json().data[1]` is the first element, while the path strings of
  `nested.property` and `have.json_body` stay 0-based (`data[0].id`). JSON arrays carry their length in `n`, `null` is `pm.null`.
- **Synchronous `pm.sendRequest`.** It returns `res, err`; with a callback, the callback runs before it returns.
- **No `done`, no promises, no `postman.*`.** Use `pm.*` only.
- **Reduced standard library.** Only `base` (without `dofile`, `loadfile`, `require`, `print`, `warn`), `string`, `table`,
  `math` and `utf8` are loaded; `collectgarbage` accepts only `"count"` and chunks load in text mode only. Use `console.*`
  for output.
- **Unsupported.** `pm.execution`, `pm.iterationData`, `pm.vault`, `pm.visualizer`, `pm.require`, `pm.request.auth`,
  `pm.response.to.have.json_schema`, `pm.cookies.jar()` and `instance_of` raise `... is not supported in Yaapic`; `require`, `os`
  and `io` are not available (indexing them fails with a standard Lua `nil` error).

| JavaScript | Lua |
|---|---|
| `pm.response.to.be.ok` | `pm.response.to.be.ok()` |
| `pm.response.to.have.jsonBody('a', 1)` | `pm.response.to.have.json_body('a', 1)` |
| `pm.expect(x).to.not.equal(1)` | `pm.expect(x).to.is_not.equal(1)` |
| `pm.expect(x).to.be.null` | `pm.expect(x).to.is_null()` |
| `pm.expect(x).to.be.above(1).and.below(5)` | `pm.expect(x).to.be.above(1).also.below(5)` |
| `pm.expect(s).to.match(/^a\d+$/)` | `pm.expect(s).to.match('^a%d+$')` |
| `pm.response.responseTime` | `pm.response.response_time` |
| `pm.variables.replaceIn('{{$guid}}')` | `pm.variables.replace_in('{{$guid}}')` |
| `pm.collectionVariables.set(k, v)` | `pm.collection_variables.set(k, v)` |
| `pm.request.url.toString()` | `pm.request.url.to_string()` |
| `pm.request.headers.upsert({ key: 'A', value: 'b' })` | `pm.request.headers.upsert({ key = 'A', value = 'b' })` |
| `pm.response.json().data[0]` | `pm.response.json().data[1]` |
| `await pm.sendRequest(url)` | `local res, err = pm.sendRequest(url)` |
| `pm.sendRequest(url, (err, res) => ...)` | `pm.sendRequest(url, function(err, res) ... end)` |
| `try { pm.response.json() } catch (e) {}` | `pcall(pm.response.json)` |
| `console.log(obj)` (JSON) | `console.log(t)` (`{ a = 1, b = { 1, 2 } }`) |

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
- **Collections and strings.** `include`/`contain` (string substring, array member, object subset, `deep.include`),
  `include.members`, `members`, `ordered.members`, `deep.members`, `property(name[, value])` (the subject becomes the property
  value, so `.property('a').that.equals(1)` works), `nested.property(path)`, `own.property`, `own_property`, `keys(...)` (`any`,
  `all`, `contain`; a list or varargs), `string`, `one_of` (`oneOf`), `satisfy`, `throw([text])`. `instance_of` is not supported.
  `match(pattern)` takes a **Lua pattern**, not a regex (its failure message shows the pattern as a string); `throw(text)`
  checks that the error message contains the text.
- **Paths use JavaScript-style 0-based indices** (`data[0].id`, also `data.0.id`) in `nested.property` and `have.json_body`,
  because they address the JSON document, not a Lua table (`pm.response.json().data[1]` is the first element in Lua).
- **`pm.response.to`** is call-style: `have.status(codeOrText)`, `have.header(name[, value])`, `have.body([text])` (exact text, or
  a Lua pattern that matches), `have.json_body([path[, value]])`, and zero-argument flags `be.ok()`, `be.success()`,
  `be.client_error()`, `be.with_body()`, `be.json()`, ... Negate with `is_not`; multi-word names also exist in camelCase
  (`jsonBody`, `clientError`). `json_schema` is unsupported. Failure messages equal the JavaScript ones (flag names use the
  camelCase spelling, e.g. `expected response to be clientError but got 200`).

## Lua differences (`pm.sendRequest` and `pm.cookies`)

- **`pm.sendRequest(input[, callback])`** (alias `pm.send_request`) blocks and **returns `res, err`**; exactly one is `nil`
  (`err` is a string). `input` is a URL string or a table `{ url, method, header, body }` (`header` an array of
  `{ key =, value = }` or a map; `body` `{ mode = "raw"|"urlencoded", raw =, urlencoded = {...} }`). There is no
  deferred delivery in Lua: with a callback, it runs immediately, before `sendRequest` returns, as `callback(err, res)`
  (same argument order as JavaScript), its result is ignored, and an error it raises is a normal script error.
- **`pm.cookies`** and `res.cookies` are read-only with snake_case names (`get`, `has`, `to_object`, `each`, `count`,
  `all`; camelCase aliases exist). Cookie entries expose `http_only` and `httpOnly`. `jar()` raises
  `The cookie jar is not available in Yaapic yet`.

## Reporting a gap

Open an issue with the script (or the smallest piece that fails), the Postman behavior you expect, and what Yaapic
printed in the console. A fix starts as a new case in the conformance suite.
