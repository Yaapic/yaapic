# M2-05: Request resolution: methods, bodies and settings

Status: todo
Platform: any
Depends on: M2-04

## Goal

`RequestResolver` turns every method, body kind and the settings into a `ResolvedRequest`.

## Design decisions

- `ResolvedRequest` gains trailing members with defaults: `IReadOnlyList<ResolvedPart>? Parts = null`,
  `string? BodyFilePath = null`, `bool FollowRedirects = true`, `int TimeoutSeconds = 0`. New
  `public sealed record ResolvedPart(string Name, string? Value, string? FilePath)` (a text part has `Value`, a file
  part has `FilePath`).
- A body is sent only when `Method.AllowsBody()` and `Body.Kind != None` (GET, HEAD, OPTIONS never send one, as GET in
  M1). Variables are substituted in field keys and values, the file path and the raw text.
- Raw: content types `application/json`, `application/xml`, `text/plain; charset=utf-8`, `text/html; charset=utf-8`
  (Html), `application/javascript` (JavaScript). A user `Content-Type` header overrides and is removed from the header
  list, as in M1.
- UrlEncoded: enabled fields with a non-empty key after substitution, each key and value `Uri.EscapeDataString`,
  joined by `&`, order kept; `BodyText` is that string, content type `application/x-www-form-urlencoded` (same user
  override rule).
- FormData: enabled fields with a non-empty key become `Parts` (text field: `Value`; file field: `FilePath`); `BodyText`
  and `BodyContentType` stay null; any user `Content-Type` header is dropped (the transport sets the boundary).
- Binary: `BodyFilePath` = substituted path (skipped when empty), content type `application/octet-stream` unless a user
  `Content-Type` header exists.
- `FollowRedirects` and `TimeoutSeconds` copy from `request.Settings`.
- Unresolved variables from all of these are reported once, in order of first occurrence, as for existing fields.

## Out of scope

Authentication (M2-06), reading files, the transport (M2-07).

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests`: PUT/PATCH/DELETE send a raw body; HEAD and OPTIONS do not; each raw content type;
      urlencoded escaping (space, `&`, unicode; disabled and empty-key fields skipped); form-data text and file parts;
      binary path; user Content-Type override for raw/urlencoded/binary and drop for form-data; variables in every
      field; unresolved names collected; settings copied; every M1 resolver test still green.
- [ ] 0 warnings.

## Suggested commit message

`feat(core): resolve all methods, body kinds and settings`
