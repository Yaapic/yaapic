# Scripting compatibility with Postman (JavaScript)

Yaapic runs scripts on QuickJS-NG with a `yaapic` object whose API follows Postman's `pm` (ADR [0009](adr/0009-scripting.md)); a script written for Postman replaces `pm` with `yaapic`, and a converter is planned. This page lists
what is supported and what differs from Postman. It is kept in step with the conformance suite
`tests/Yaapic.Scripting.Tests/Conformance/JavaScriptConformanceTests.cs` (and, for Lua, `LuaConformanceTests.cs`): every
`supported` row is covered by a case there, and every `unsupported` row by an unsupported-member case.

## The `yaapic` surface

| Member | Status | Note |
|---|---|---|
| `yaapic.test`, `yaapic.expect` | supported | Chai-style chains: `property`, `include`, `an`, `lengthOf`, `match`, `oneOf`, `eql`, `below`, `true`/`false`, `ok`... A failed `yaapic.test` is a test result with the assertion message, not a script error. |
| `yaapic.response.code`, `status`, `responseTime`, `text()`, `json()`, `headers` | supported | `headers.get` is case-insensitive. `json()` throws on a non-JSON body, as in Postman. |
| `yaapic.response.to.have.status`, `header`, `jsonBody`; `to.be.ok` | supported | |
| `yaapic.response.to.have.jsonSchema` | unsupported | Fails with `... is not supported in Yaapic`. |
| `yaapic.request.method`, `url`, `headers`, `body` | supported | Read in both phases, mutable in pre-request (`headers.add/upsert/remove`, `url.query.add`, `body.raw`). |
| `yaapic.request.auth` | unsupported | Use a header in a pre-request script. |
| `yaapic.variables`, `yaapic.environment`, `yaapic.collectionVariables`, `yaapic.globals` | supported | `get`, `set`, `unset`, `has`, `clear`, `toObject`; `replaceIn` on `yaapic.variables` (dynamic variables such as `{{$guid}}`, `{{$timestamp}}`, `{{$randomInt}}` work). |
| `yaapic.environment.name` | unsupported | |
| `yaapic.info` | partial | `eventName`, `requestName`, `requestId`; `iteration` is always `0` and `iterationCount` `1`. |
| `yaapic.sendRequest` | supported | String and options forms; callback and promise (`await`) styles; raw and urlencoded bodies. Limited to 20 calls per run. |
| `yaapic.cookies` | partial | Read-only view of the `Set-Cookie` headers of the current response. `yaapic.cookies.jar()` throws `The cookie jar is not available in Yaapic yet`. |
| `console.*` | supported | Objects are printed as JSON. |
| `postman.setEnvironmentVariable`, `getEnvironmentVariable`, `clearEnvironmentVariable`, and the global equivalents | supported | Aliases of the `yaapic` calls. |
| `postman.setNextRequest`, `yaapic.execution` | unsupported | Needs the Runner (milestone 6). |
| `yaapic.iterationData` | unsupported | Needs the Runner (milestone 6). |
| `yaapic.vault` | unsupported | |
| `yaapic.visualizer` | unsupported | |
| `yaapic.require` | unsupported | |
| `require('crypto-js')`, `CryptoJS` | unsupported | `require` and `CryptoJS` exist as stubs that throw `... is not supported in Yaapic`. |

Unsupported members throw an `Error` whose message is `<member> is not supported in Yaapic`, so a collection imported from
Postman never fails silently.

The language itself is QuickJS: ES2023 syntax (template literals, destructuring, classes with private fields, optional
chaining, `Array.prototype.flat`, `Object.entries`, `async`/`await` at the top level, `toFixed`) works. There is no `Intl`,
no timers (`setTimeout`), no `BigInt`, `WeakRef` or `DOMException`, and no host module (`std`, `os` and `require` are
undefined or throw as above).

## Differences from Postman

- **Layered write fallback.** `yaapic.environment.set`, `yaapic.collectionVariables.set` and `yaapic.globals.set` write to their scope
  and are persisted after the send. When the scope does not exist (no active environment, or a request outside a
  collection), the value lives for that send only and a `warn` line is written to the console.
- **No bundled libraries.** `CryptoJS`, `lodash`, `moment`, `cheerio`, `tv4`, `uuid` and `atob`/`btoa` helpers are not
  provided; use plain JavaScript (for example a hand-rolled checksum) or `{{$guid}}` for identifiers.
- **Cookies.** `max-age` is ignored when reading `Set-Cookie` headers; there is no cookie jar.
- **Scripts see templates.** In pre-request, `yaapic.request.url` and the headers keep their `{{variables}}`; call
  `yaapic.variables.replaceIn` to resolve a string. The sender resolves the mutated request afterwards.
- **Errors.** A pre-request error or timeout (5 s per block) aborts the send; a post-response error keeps the response and
  the tests already recorded.

## Lua overview

Scripts written in Lua 5.4 use the same `yaapic` object with Lua naming; the Lua differences below are checked by
`LuaConformanceTests.cs`, which also runs a mixed-language chain (JavaScript collection script, Lua folder and request
scripts, JavaScript request test) and shows that variables and the response are shared across languages.

- **Naming.** Functions are `snake_case` (`replace_in`, `to_string`, `response_time`, `json_body`); the JavaScript camelCase names
  also exist for most of them (`replaceIn`, `jsonBody`).
- **Reserved words.** Chai's `not` and `and` are `is_not` and `also`.
- **Call-style assertions.** Property assertions are calls: `yaapic.response.to.be.ok()`, `yaapic.expect(x).to.is_null()`.
- **Patterns, not regexes.** `match(pattern)` and `have.body(pattern)` take Lua patterns (`%d+`, not `\d+`).
- **1-based tables, 0-based JSON paths.** `yaapic.response.json().data[1]` is the first element, while the path strings of
  `nested.property` and `have.json_body` stay 0-based (`data[0].id`). JSON arrays carry their length in `n`, `null` is `yaapic.null`.
- **Synchronous `yaapic.sendRequest`.** It returns `res, err`; with a callback, the callback runs before it returns.
- **No `done`, no promises, no `postman.*`.** Use `yaapic.*` only.
- **Reduced standard library.** Only `base` (without `dofile`, `loadfile`, `require`, `print`, `warn`), `string`, `table`,
  `math` and `utf8` are loaded; `collectgarbage` accepts only `"count"` and chunks load in text mode only. Use `console.*`
  for output.
- **Unsupported.** `yaapic.execution`, `yaapic.iterationData`, `yaapic.vault`, `yaapic.visualizer`, `yaapic.require`, `yaapic.request.auth`,
  `yaapic.response.to.have.json_schema`, `yaapic.cookies.jar()` and `instance_of` raise `... is not supported in Yaapic`; `require`, `os`
  and `io` are not available (indexing them fails with a standard Lua `nil` error).

| JavaScript | Lua |
|---|---|
| `yaapic.response.to.be.ok` | `yaapic.response.to.be.ok()` |
| `yaapic.response.to.have.jsonBody('a', 1)` | `yaapic.response.to.have.json_body('a', 1)` |
| `yaapic.expect(x).to.not.equal(1)` | `yaapic.expect(x).to.is_not.equal(1)` |
| `yaapic.expect(x).to.be.null` | `yaapic.expect(x).to.is_null()` |
| `yaapic.expect(x).to.be.above(1).and.below(5)` | `yaapic.expect(x).to.be.above(1).also.below(5)` |
| `yaapic.expect(s).to.match(/^a\d+$/)` | `yaapic.expect(s).to.match('^a%d+$')` |
| `yaapic.response.responseTime` | `yaapic.response.response_time` |
| `yaapic.variables.replaceIn('{{$guid}}')` | `yaapic.variables.replace_in('{{$guid}}')` |
| `yaapic.collectionVariables.set(k, v)` | `yaapic.collection_variables.set(k, v)` |
| `yaapic.request.url.toString()` | `yaapic.request.url.to_string()` |
| `yaapic.request.headers.upsert({ key: 'A', value: 'b' })` | `yaapic.request.headers.upsert({ key = 'A', value = 'b' })` |
| `yaapic.response.json().data[0]` | `yaapic.response.json().data[1]` |
| `await yaapic.sendRequest(url)` | `local res, err = yaapic.sendRequest(url)` |
| `yaapic.sendRequest(url, (err, res) => ...)` | `yaapic.sendRequest(url, function(err, res) ... end)` |
| `try { yaapic.response.json() } catch (e) {}` | `pcall(yaapic.response.json)` |
| `console.log(obj)` (JSON) | `console.log(t)` (`{ a = 1, b = { 1, 2 } }`) |

## Lua differences (request and response)

- **Setters instead of assignment.** The `yaapic.request`, `yaapic.request.url`, `yaapic.request.body` and `yaapic.response` tables are read-only,
  so `yaapic.request.method = 'POST'` raises an error. Use `yaapic.request.set_method(m)`, `yaapic.request.set_url(s)`,
  `yaapic.request.url.update(s)`, `yaapic.request.body.set_mode(m)` and `yaapic.request.body.set_raw(s)`. Reading `yaapic.request.method`
  and `yaapic.request.body.mode` / `raw` still works, as do the `get_*` functions.
- **No URL part setters.** `yaapic.request.url.protocol`, `host`, `port`, `path` and `hash` are read-only (arrays are 1-based tables);
  rebuild the URL with `update`.
- **Lists.** `headers`, `url.query`, `body.urlencoded`, `body.formdata` and `yaapic.response.headers` share one list API
  (`get`, `has`, `add{...}`, `upsert{...}`, `remove`, `each`, `count`, `all`, `idx` (1-based), `to_object`); every function is
  called with a dot, and both `snake_case` and `camelCase` names exist.
- **`yaapic.response` in pre-request.** Fetching `yaapic.response` itself does not fail; the first member access raises
  `yaapic.response is only available in post-response scripts`.

## Lua differences (`yaapic.test` and `yaapic.expect`)

- **Every assertion is a call.** `yaapic.expect(x).to.equal(1)`, `yaapic.expect(x).to.be.ok()`. Chai property assertions become
  zero-argument calls: `ok()`, `is_true()`, `is_false()`, `is_null()` (matches `yaapic.null`), `is_nil()` (matches `nil`),
  `is_nan()`, `exist()`, `empty()`.
- **Reserved words.** `not` is `is_not` and `and` is `also` (`yaapic.expect(x).to.is_not.equal(1)`, `.above(1).also.below(5)`);
  `deep` is a property (`deep.equal`). `a(type)` accepts `string`, `number`, `integer`, `float`, `boolean`, `table`, `array`,
  `object`, `function`, `nil` and `null`.
- **Names.** `close_to` (`closeTo`), `length` (`length_of`, `lengthOf`), `gt`, `gte`, `lt`, `lte`; there is no `lengthOf.above(n)`
  chain.
- **Equality.** `equal` is `==` (tables by identity); `eql` is deep (arrays by length, objects by keys, `NaN` never equals `NaN`).
- **Failure messages are the Chai texts** (`expected 409 to equal 204`), with values rendered like JavaScript (`nil` is
  `undefined`, `yaapic.null` is `null`, `{ a = 1 }` is `{ a: 1 }`), so a failure reads the same in both languages.
- **`yaapic.test`.** `yaapic.test(name, fn)` runs `fn` under `pcall`; there is no `done` callback and no promise support. A failed test's
  message is `AssertionError: ...`, or the error text without its `origin:line:` prefix (the console keeps the position).
  Test durations are recorded as 0 ms.
- **Collections and strings.** `include`/`contain` (string substring, array member, object subset, `deep.include`),
  `include.members`, `members`, `ordered.members`, `deep.members`, `property(name[, value])` (the subject becomes the property
  value, so `.property('a').that.equals(1)` works), `nested.property(path)`, `own.property`, `own_property`, `keys(...)` (`any`,
  `all`, `contain`; a list or varargs), `string`, `one_of` (`oneOf`), `satisfy`, `throw([text])`. `instance_of` is not supported.
  `match(pattern)` takes a **Lua pattern**, not a regex (its failure message shows the pattern as a string); `throw(text)`
  checks that the error message contains the text.
- **Paths use JavaScript-style 0-based indices** (`data[0].id`, also `data.0.id`) in `nested.property` and `have.json_body`,
  because they address the JSON document, not a Lua table (`yaapic.response.json().data[1]` is the first element in Lua).
- **`yaapic.response.to`** is call-style: `have.status(codeOrText)`, `have.header(name[, value])`, `have.body([text])` (exact text, or
  a Lua pattern that matches), `have.json_body([path[, value]])`, and zero-argument flags `be.ok()`, `be.success()`,
  `be.client_error()`, `be.with_body()`, `be.json()`, ... Negate with `is_not`; multi-word names also exist in camelCase
  (`jsonBody`, `clientError`). `json_schema` is unsupported. Failure messages equal the JavaScript ones (flag names use the
  camelCase spelling, e.g. `expected response to be clientError but got 200`).

## Lua differences (`yaapic.sendRequest` and `yaapic.cookies`)

- **`yaapic.sendRequest(input[, callback])`** (alias `yaapic.send_request`) blocks and **returns `res, err`**; exactly one is `nil`
  (`err` is a string). `input` is a URL string or a table `{ url, method, header, body }` (`header` an array of
  `{ key =, value = }` or a map; `body` `{ mode = "raw"|"urlencoded", raw =, urlencoded = {...} }`). There is no
  deferred delivery in Lua: with a callback, it runs immediately, before `sendRequest` returns, as `callback(err, res)`
  (same argument order as JavaScript), its result is ignored, and an error it raises is a normal script error.
- **`yaapic.cookies`** and `res.cookies` are read-only with snake_case names (`get`, `has`, `to_object`, `each`, `count`,
  `all`; camelCase aliases exist). Cookie entries expose `http_only` and `httpOnly`. `jar()` raises
  `The cookie jar is not available in Yaapic yet`.

## Reporting a gap

Open an issue with the script (or the smallest piece that fails), the Postman behavior you expect, and what Yaapic
printed in the console. A fix starts as a new case in the conformance suite.
