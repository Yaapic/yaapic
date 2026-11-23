# M5-65: End-to-end tests: import

Status: done
Platform: any
Depends on: M5-54

## Goal

Import every supported format through the real shell on a temporary workspace, then send an imported request to the loopback test server.

## Design decisions

- Read first: `tests/Yaapic.Infrastructure.Tests/EndToEnd/` (`Milestone2EndToEndTests`, `Milestone3ScriptingEndToEndTests`, `Milestone4*`, `ScriptedShellFixture`, the loopback `HttpListener` server with echo, `/status/{code}`), M5-54, M5-51.
- `tests/Yaapic.Infrastructure.Tests/EndToEnd/Milestone5ImportEndToEndTests.cs` (+ fixture files embedded as raw string literals in `tests/Yaapic.Infrastructure.Tests/EndToEnd/Milestone5Samples.cs`). The shell is built like the milestone 3 tests (real engines, real vault, `FakeDialogService`); the import goes through `shell.CreateImport()`, `PreviewPastedCommand` or `ChooseFileCommand`
  (write sample files to the temp directory and set `FakeDialogService.PickFileAnswer`; `PickFolderAnswer` for Bruno), then `ImportCommand`. Samples (small but realistic, each in its own constant): a Postman v2.1 collection (folder, GET with params, POST JSON, bearer auth at collection level, a pre-request script, collection variable `baseUrl`),
  a Postman environment, an OpenAPI 3 YAML file (two tags, a path parameter, a JSON request body schema, a bearer security scheme), the same API as Swagger 2.0 JSON, a cURL command (browser bash form), a HAR with five entries (one image, one OPTIONS preflight, one duplicate), an Insomnia v4 export (base and sub environment, template tags), a Bruno folder (bruno.json, folder.bru, two requests, environments with a secret).
- Scenarios (each asserts the workspace through the stores and the tree, not only the report): every import above lands the expected folders, request count, URLs, auth, body kinds and variables; the report `Warning`/`Info` counts for each; an imported Postman script runs (send the request to the loopback server, the post-response script sets a variable);
  a request imported from cURL is sent to the loopback server and the echoed headers and body match; a secret environment row imports with a **locked vault** (blank + `Warning`) and with an **unlocked** one (value in the vault, blank in the YAML file on disk: read the file); importing the same collection twice creates `Name (2)`;
  the HAR static filter and the `IncludeStaticResources` option; auto-detection picks the right importer for each sample (assert `DetectedFormat`); garbage text gives the "not recognized" error and a manual format choice then fails with a format error, not a crash; a 5001-file folder is refused;
  the 20000-request limit is refused (generate a Postman JSON in memory); a malicious `$ref` to a URL and to a file path in OpenAPI is reported and nothing is read or fetched (assert the loopback server received no request); a file reference in a multipart body carries the `Warning`.
- Tests that need the native script engines follow the existing skip rules of the milestone 3 tests.

## Out of scope

UI tests, exports (M5-66).

## Acceptance criteria

- [x] The test class with the scenarios above passes locally and in CI configuration (no external network).
- [x] Architecture tests green, 0 warnings, all tests green.

## Suggested commit message

`test: add end-to-end tests for importing Postman, OpenAPI, cURL, HAR, Insomnia and Bruno`
