# M1-01: Domain request model

Status: todo
Platform: any
Depends on: none

## Goal

The immutable request model in `Yaapic.Domain`, plus the first domain test project `tests/Yaapic.Domain.Tests`.

## Design decisions

- Namespace `Yaapic.Domain.Http`, folder `src/Yaapic.Domain/Http/`, one public type per file, all `sealed record` or
  `enum`, no package dependency.
- `enum HttpMethodKind { Get, Post }` (name avoids a clash with `System.Net.Http.HttpMethod`; more methods come in M2).
- `sealed record KeyValueEntry(string Key, string Value, bool Enabled = true)`. Reused for query params, headers,
  response headers and variables.
- `enum BodyKind { None, Raw }`, `enum RawContentType { Text, Json, Xml }`.
- `sealed record RequestBody(BodyKind Kind, RawContentType ContentType, string Text)` with
  `static RequestBody None { get; }` (`Kind = None`, `ContentType = Text`, `Text = ""`) and
  `static RequestBody Raw(RawContentType type, string text)`.
- `sealed record HttpRequestDefinition(Guid Id, string Name, HttpMethodKind Method, string Url,
  IReadOnlyList<KeyValueEntry> QueryParams, IReadOnlyList<KeyValueEntry> Headers, RequestBody Body)`.
  `Url` is the text as typed (may contain `{{variables}}` and its own query string); `QueryParams` are appended on send
  (M1-06).
- `static HttpRequestDefinition Create(string name)`: new `Guid`, `Get`, empty URL, empty lists, `RequestBody.None`;
  throws `ArgumentException` when `name` is null or whitespace; stores `name.Trim()`.
- `bool IsEquivalentTo(HttpRequestDefinition other)`: true when every property is equal, comparing the two lists element
  by element with `SequenceEqual` (record equality alone compares lists by reference). Used for dirty tracking (M1-17).
- `tests/Yaapic.Domain.Tests`: copy the settings of `tests/Yaapic.Architecture.Tests/Yaapic.Architecture.Tests.csproj`
  (xunit.v3, Microsoft.Testing.Platform runner, `IsAotCompatible` false, `IsPackable` false), reference only
  `Yaapic.Domain`, add it to `Yaapic.sln`.

## Out of scope

Other HTTP methods, form/multipart/binary bodies, auth, URL validation, storage, serialization attributes.

## Acceptance criteria

- [ ] Types above exist in `src/Yaapic.Domain/Http/`, build with 0 warnings.
- [ ] Tests: `Create` trims and rejects blank names; two `Create` calls give distinct ids; `IsEquivalentTo` is true for
      equal content held in different list instances and false after changing each of: name, method, url, a param, a
      header, the body.
- [ ] `Yaapic.Architecture.Tests` still green (Domain references nothing).

## Suggested commit message

`feat(domain): add HTTP request model`
