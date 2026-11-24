# M6-27: Domain: mock server model and validation

Status: todo
Platform: any
Depends on: M6-04

## Goal

The mock server definition of ADR 0020 as domain records, with validation.

## Design decisions

- Read first: ADR 0020 ("File", "Matching"), `src/Yaapic.Domain/Environments/EnvironmentDefinition.cs` (record style, `Create`),
  `src/Yaapic.Domain/Http/KeyValueEntry.cs`, `src/Yaapic.Domain/Http/ClientCertificateValidator.cs` (a validator returning issues).
- Folder `src/Yaapic.Domain/Mocks/`:
  - `MockRoute(Guid Id, string Name, bool Enabled, string Method, string Path, int Status, IReadOnlyList<KeyValueEntry> Headers,
    string Body, int DelayMs)` with `static MockRoute Create(string name)` (new id, enabled, `GET`, `/`, 200, header
    `Content-Type: application/json`, body `{}`, delay 0).
  - `MockServerDefinition(Guid Id, string Name, int Port, bool Cors, IReadOnlyList<MockRoute> Routes)` with
    `static MockServerDefinition Create(string name, int port = 3000)` (cors true, no route) and `bool IsEquivalentTo(other)` (like
    `EnvironmentDefinition`, sequence-equal routes and headers).
  - `MockMethods` (static): `Any = "ANY"`, `IReadOnlyList<string> All` = `GET`, `POST`, `PUT`, `PATCH`, `DELETE`, `HEAD`, `OPTIONS`, `ANY`.
  - `MockIssue` enum: `NameEmpty`, `PortOutOfRange`, `RouteMethodInvalid`, `RoutePathInvalid`, `RouteStatusOutOfRange`,
    `RouteDelayOutOfRange`, `RouteHeaderNameInvalid`; `MockValidationIssue(MockIssue Issue, Guid? RouteId)`.
  - `MockServerValidator.Validate(MockServerDefinition)` -> `IReadOnlyList<MockValidationIssue>`: name not blank; port 1024..65535;
    method in `MockMethods.All` (ordinal, upper case); path starts with `/`, has no `?`, `#`, whitespace or control character, and `*`
    only as a whole last segment; status 100..599; delay 0..60000; enabled header names are HTTP tokens (RFC 9110 `tchar`), disabled
    rows are not checked.

## Out of scope

Storage (M6-28), matching (M6-29).

## Acceptance criteria

- [ ] Tests in `Yaapic.Domain.Tests/Mocks/`: `Create` defaults; each issue, with the route id; a valid definition has no issue; `*` in the
  middle of a path is invalid, `/*` and `/files/*` are valid; `:id` and `{id}` segments are valid; `IsEquivalentTo`.
- [ ] Architecture tests green (Domain depends on nothing), 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(domain): add the mock server model`
