# M2-02: Domain: HTTP methods and raw content types

Status: todo
Platform: any
Depends on: M2-01

## Goal

The domain knows every HTTP method and raw content type of milestone 2.

## Design decisions

- `HttpMethodKind` in `Yaapic.Domain.Http` gains `Put`, `Patch`, `Delete`, `Head`, `Options` (after `Post`, with XML
  docs). The values of `Get` and `Post` do not change.
- `RawContentType` gains `Html` and `JavaScript`.
- New `public static class HttpMethodKindExtensions` in the same folder:
  - `string ToText(this HttpMethodKind)`: `GET`, `POST`, `PUT`, `PATCH`, `DELETE`, `HEAD`, `OPTIONS`.
  - `bool TryParse(string? text, out HttpMethodKind method)`: upper-case ordinal match, no trimming.
  - `bool AllowsBody(this HttpMethodKind)`: true for Post, Put, Patch, Delete; false for Get, Head, Options.
- Every `switch` on these enums elsewhere keeps compiling; behaviour changes belong to later cards (M2-05, M2-08).
  Do not touch Core, Infrastructure, ViewModels or App projects here, except to make the build pass without changing
  behaviour.

## Out of scope

Resolver, transport, serializer, ViewModel and view changes.

## Acceptance criteria

- [ ] Tests in `Yaapic.Domain.Tests`: `ToText` and `TryParse` round trip for all seven methods; `TryParse` rejects
      `"get"`, `""`, `null` and `"TRACE"`; `AllowsBody` truth table.
- [ ] 0 warnings.

## Suggested commit message

`feat(domain): add all HTTP methods and html/javascript content types`
