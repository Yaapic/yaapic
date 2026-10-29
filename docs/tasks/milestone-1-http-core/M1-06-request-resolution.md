# M1-06: Request resolution

Status: done
Platform: any
Depends on: M1-01, M1-04

## Goal

Turn a `HttpRequestDefinition` into a ready-to-send `ResolvedRequest`: variables, query string, headers, body.

## Design decisions

- Namespace `Yaapic.Core.Http`, folder `src/Yaapic.Core/Http/`.
- `sealed record ResolvedRequest(HttpMethodKind Method, string Url, IReadOnlyList<KeyValueEntry> Headers, string? BodyText,
  string? BodyContentType, IReadOnlyList<string> Unresolved)`.
- `static class RequestResolver { public static ResolvedRequest Resolve(HttpRequestDefinition request, IVariableSource source); }`.
- Steps, in order:
  1. Resolve `Url`, then trim it. Empty stays empty. If it does not contain `://`, prefix `http://`.
  2. Params and headers: drop disabled entries; resolve key and value; then drop entries whose resolved key is empty.
  3. Append params to the URL: split at the first `#` (the fragment goes back at the end); if the URL has no `?` add `?`,
     otherwise add `&` unless it already ends with `?` or `&`; each pair is
     `Uri.EscapeDataString(key)=Uri.EscapeDataString(value)`, pairs joined by `&`. No params: URL unchanged.
  4. Body: kept only when `Method == Post` and `Body.Kind == Raw`; its text is resolved. Otherwise `BodyText` and
     `BodyContentType` are null (a GET never carries a body).
  5. `BodyContentType` default: `Text` -> `text/plain; charset=utf-8`, `Json` -> `application/json`, `Xml` ->
     `application/xml`. If the resolved headers contain `Content-Type` (name ordinal-ignore-case), the first such header
     wins for a POST with a body: `BodyContentType` takes its value and every `Content-Type` header is removed from
     `Headers` (the transport sets it on the content). Without a body, a user `Content-Type` header stays in `Headers`.
  6. `Unresolved` is the distinct union, in first-occurrence order, over URL, params, headers, body.
- Header names keep their case as typed.

## Out of scope

URL validation (the transport reports `InvalidUrl`), auth, form bodies.

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests`: scheme prefix; params appended with `?`, with `&`, after a trailing `?`; params placed
      before `#fragment`; encoding of space, `&`, `=` and non-ASCII; disabled and empty-key entries dropped; variables in
      URL, key, value and body; GET with a Raw body drops it; POST default content types for the three kinds; user
      `Content-Type` override; unresolved aggregation order.
- [x] 0 warnings.

## Suggested commit message

`feat(core): resolve requests against variables`
