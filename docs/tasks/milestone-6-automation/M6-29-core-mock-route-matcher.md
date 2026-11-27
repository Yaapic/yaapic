# M6-29: Core: mock HTTP types and route matcher

Status: done
Platform: any
Depends on: M6-27

## Goal

The request and response types the mock server exchanges, and the route matching of ADR 0020 ("Matching").

## Design decisions

- Read first: ADR 0020, M6-27.
- Folder `src/Yaapic.Core/Mocks/`:
  - `MockHttpRequest(string Method, string RawTarget, string Path, string Query, IReadOnlyList<KeyValueEntry> Headers, byte[] Body,
    string HttpVersion)`: `RawTarget` as received (`/users/1?x=2`), `Path` percent-decoded without query, `Query` without `?`.
    `static MockHttpRequest Parse(string method, string rawTarget, IReadOnlyList<KeyValueEntry> headers, byte[] body, string version)`
    splits the target (an absolute-form target `http://host/path` keeps only the path), decodes `%XX` (UTF-8; invalid sequences kept
    as written), and never throws.
    `string? Header(string name)` (first, case-insensitive), `IReadOnlyList<KeyValueEntry> QueryValues` (split on `&` and `=`,
    `+` as space, percent-decoded).
  - `MockHttpResponse(int Status, string Reason, IReadOnlyList<KeyValueEntry> Headers, byte[] Body, TimeSpan Delay, bool OmitBody = false)`.
  - `MockMatch(MockRoute Route, IReadOnlyDictionary<string, string> Params)`.
  - `MockRouteMatcher.Match(IReadOnlyList<MockRoute> routes, string method, string path)` -> `MockMatch?`: first enabled route whose
    method equals the request method (ordinal, upper case) or is `ANY`, or is `GET` when the request is `HEAD`, and whose template
    matches. Trailing `/` ignored on both sides except for `/`. Segment rules: literal (ordinal), `:name` or `{name}` (any non-empty
    segment, captured decoded), `*` (zero or more remaining segments, captured joined with `/` under the key `*`). Empty segments from
    `//` are kept as empty literals (so `/a//b` does not match `/a/b`).
  - `string ReasonPhrases.For(int status)` (a small table of the common codes; others "Status").

## Out of scope

Templating and responses (M6-30), the server (M6-32, M6-33).

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests/Mocks/`: `Parse` (query split, decoding, `+`, absolute-form, invalid `%`); `Match` with literals,
  `:id`, `{id}`, `*` at the root and after a prefix, trailing slashes, method `ANY`, `HEAD` on a `GET` route, disabled routes skipped,
  first match wins over a later more specific route, no match returns null, case-sensitive literals.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): match mock routes`
