# M3-21: JavaScript `pm.expect` collections/strings and `pm.response.to.*`

Status: done
Platform: any
Depends on: M3-20

## Goal

The second half of the Chai-style assertions and the Postman response assertion helpers.

## Design decisions

- Read first: M3-20 (`Assertion`, `inspect`, message style), Postman's reference for `pm.response.to`.
- Added to `PmTest.js` (or a new `PmExpect2.js` part, same embedding rule):
  - `include(v)` (`includes`, `contain`, `contains`): string substring, array member (`===`, `deep` flag -> deep equality),
    object subset (`{a:1}` is included in `{a:1,b:2}`), Map/Set membership; `include.members` via `members`.
  - `property(name[, value])` (own or inherited; `nested.property('a.b[0].c')`; `deep.property`), `ownProperty(name)`,
    `own.property`, `haveOwnProperty`; after `property` the assertion subject becomes the property value (Chai chaining:
    `expect(o).to.have.property('a').that.equals(1)`).
  - `keys(...)` / `key` (exact set by default, `include`/`contain` for a subset, `any`/`all` flags), `members([..])`
    (same members, order ignored; `include.members`, `deep.members`, `ordered.members`).
  - `match(regex)` (`matches`), `string(substr)`, `oneOf(array)`, `instanceOf(ctor)` (`instanceof`), `satisfy(fn)`
    (`satisfies`), `throw([ctor|message|regex])` (`throws`, `Throw`), `respondTo` is **not** supported.
- `pm.response.to` (post-response only; each assertion records a Chai-style message, e.g.
  `expected response to have status code 200 but got 404`) with the `be`/`have` chains and `not`:
  - `have.status(codeOrText)`, `have.header(name[, value])`, `have.body(textOrRegex)`, `have.jsonBody([path[, value]])`
    (path = dotted/`[i]` path into the parsed JSON, value compared deeply), `have.jsonSchema` is **unsupported**
    (`"pm.response.to.have.jsonSchema is not supported in Yaapic"`, schema validation is a later card).
  - state flags: `ok` (200), `success` (2xx), `info` (1xx), `redirection` (3xx), `clientError` (4xx), `serverError` (5xx),
    `error` (4xx or 5xx), `accepted` (202), `badRequest` (400), `unauthorized` (401), `forbidden` (403), `notFound` (404),
    `rateLimited` (429), `withBody`, `json` (content type JSON), `html`, `text`.
- `pm.expect(pm.response.code).to.be.oneOf([200, 201])` and the other combos must work with the chain from M3-20.

## Out of scope

JSON schema validation, `chai.use` plugins, `pm.expect.fail` already done, `respondTo`.

## Acceptance criteria

- [x] Tests (real QuickJS): each assertion above passing, failing (exact Chai-style message) and negated; chaining
      (`property` then `that.equals`); nested property paths; deep include; keys any/all/include; members ordered/deep;
      `throw` with constructor, message and regex; every `pm.response.to` helper against 200/201/202/301/400/401/403/404/
      429/500 responses with and without body, JSON and HTML content types; `have.jsonBody` paths; `have.jsonSchema` error
      message; `pm.response.to` in pre-request throws the post-response-only error.
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

`feat(scripting): add remaining pm.expect assertions and response helpers`
