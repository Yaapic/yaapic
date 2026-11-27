# M6-31: Core: generate a mock server from a folder

Status: done
Platform: any
Depends on: M6-14, M6-27

## Goal

Build a mock server definition with one route per request of a folder (ADR 0020, "Generate from a collection").

## Design decisions

- Read first: ADR 0020, M6-14 (`RunPlanBuilder.ListRequests`: same order), `src/Yaapic.Core/Variables/VariableScanner.cs`,
  `src/Yaapic.Core/Import/ImportHelpers.cs` (URL splitting helpers from M5, reuse them if one fits).
- `src/Yaapic.Core/Mocks/MockGenerator.cs`: `static MockGeneration FromFolder(CollectionNode folder)` returning
  `MockGeneration(MockServerDefinition Server, int Skipped)`; name `Strings.MockGeneratedName(folder.Name)` ("{0} mock"), port 3000,
  cors true.
- Path of a request URL template (`static string PathOf(string url)`, public for tests):
  1. drop the fragment and the query (`#`, `?` outside `{{...}}`);
  2. remove a leading scheme and authority (`https://host:port`); otherwise remove a leading `{{name}}` token when it is immediately
     followed by `/` or the end (a base-URL variable); otherwise, when the text does not start with `/`, remove everything up to the
     first `/` (a host without a scheme);
  3. in each remaining segment that is exactly one `{{name}}` token, use `:name`; other `{{...}}` inside a segment are kept as written;
  4. empty result -> `/`.
- Each request gives `MockRoute.Create(request.Name)` with `Method = request.Method.ToText()`, `Path = PathOf(request.Url)`, status
  200, `Content-Type: application/json`, body `{}`. A route whose method and path equal an earlier one is skipped and counted.
- String (EN and FR): `mock.generatedName` = "{0} mock".

## Out of scope

The UI command (M6-35).

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests/Mocks/MockGeneratorTests.cs`: `PathOf` for `https://api.test/users/{{id}}?x=1`, `{{baseUrl}}/users`,
  `{{baseUrl}}`, `api.test/v1/items`, `/plain`, `http://host:8080`, `{{baseUrl}}/files/{{name}}.json`, empty; a nested folder gives routes in
  run order; duplicates skipped and counted; generated definition passes `MockServerValidator`.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): generate a mock server from a folder`
