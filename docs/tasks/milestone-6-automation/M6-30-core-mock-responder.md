# M6-30: Core: mock responder and templating

Status: todo
Platform: any
Depends on: M6-29

## Goal

Turn a request into the response of ADR 0020: templating, the 404 fallback, CORS and `HEAD`.

## Design decisions

- Read first: ADR 0020 ("Templating", "No match", "CORS"), M6-29, `src/Yaapic.Core/Variables/DynamicVariableSource.cs`,
  `VariableScanner.cs` (how `{{...}}` tokens are found).
- `src/Yaapic.Core/Mocks/MockTemplate.cs`: `static string Render(string template, MockHttpRequest request, IReadOnlyDictionary<string,
  string> parameters)`: replaces `{{params.x}}`, `{{query.x}}` (first value), `{{headers.x}}` (case-insensitive name), `{{body}}`
  (UTF-8 decode of the body, invalid bytes replaced) and the dynamic variables (`{{$guid}}` and the others of `DynamicVariableSource`,
  a fresh value per occurrence); a missing `params`/`query`/`headers` name gives `""`; any other token stays as written. Use the
  existing scanner if it fits, else a single left-to-right pass (no regex).
- `src/Yaapic.Core/Mocks/MockResponder.cs`: `static MockResult Respond(MockServerDefinition server, MockHttpRequest request)` with
  `public sealed record MockResult(MockHttpResponse Response, string? RouteName)` (`RouteName` null for a preflight or no match):
  1. CORS preflight (when `server.Cors`, method `OPTIONS`, header `Access-Control-Request-Method` present, and no enabled `OPTIONS` or
     `ANY` route matches): `204` with the four CORS headers of ADR 0020 (allow-headers = the request's
     `Access-Control-Request-Headers` or `*`), no body;
  2. match (M6-29): status, reason (`ReasonPhrases`), headers with rendered values (disabled rows skipped), rendered body as UTF-8,
     `Delay = route.DelayMs`; `OmitBody` when the request is `HEAD`;
  3. no match: `404`, `Content-Type: application/json`, body `{"error":"No mock route matches {METHOD} {path}"}` (JSON-escaped path);
  4. when `server.Cors`, add `Access-Control-Allow-Origin: *` unless the route already sets that header (case-insensitive).
  The responder never adds `Content-Length`, `Date` or `Server` (the server writes them, M6-32).
- `src/Yaapic.Core/Mocks/MockLogEntry.cs`: `MockLogEntry(DateTimeOffset At, string Method, string Target, int Status, string? RouteName,
  TimeSpan Duration, IReadOnlyList<KeyValueEntry> RequestHeaders, string RequestBodyPreview)` (used by M6-34; defined here so the
  responder tests can build one with `MockLogEntry.From(request, response, routeName, at, duration)`; the body preview is the first 4096
  characters of the UTF-8 body).

## Out of scope

The socket server (M6-32, M6-33), lifecycle and log storage (M6-34).

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests/Mocks/`: every template token, missing names, unknown token kept, `$guid` differs per occurrence;
  matched response with headers and delay; disabled header skipped; HEAD omits the body; 404 body exact (with a path needing JSON
  escaping); preflight answered and not answered when an `OPTIONS` route exists or CORS is off; `Access-Control-Allow-Origin` added once;
  `MockLogEntry.From` truncates the preview.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): answer mock requests`
