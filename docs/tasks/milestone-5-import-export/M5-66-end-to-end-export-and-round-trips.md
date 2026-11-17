# M5-66: End-to-end tests: export and round trips

Status: todo
Platform: any
Depends on: M5-54

## Goal

Export through the real shell and prove that each export is read back by the matching importer.

## Design decisions

- Read first: M5-65 (fixture, samples, shell construction), M5-52, M5-54, and every "Round trip" bullet of the export cards (M5-38 to M5-46): the tests those cards left to this card (because an importer card was not done yet) are written here.
- `tests/Yaapic.Infrastructure.Tests/EndToEnd/Milestone5ExportEndToEndTests.cs`. Start from a workspace built by importing the Postman, Insomnia and Bruno samples of M5-65 (so the model holds nested folders, every body kind, every auth kind, scripts, variables, a scoped environment and a **secret** variable with an unlocked vault).
- Scenarios: for each exporter (`postman`, `postman-environment`, `openapi-json`, `openapi-yaml`, `insomnia`, `bruno`) `shell.CreateExport()`, `LoadAsync`, select the format, `ExportCommand`, read `FakeDialogService.SavedFiles`:
  - **Round trips** (export, then import the bytes with the matching importer through `CreateImport()`): Postman collection and environment; Insomnia; Bruno (the zip is unpacked into an `ImportInput`); OpenAPI JSON and OpenAPI YAML. Compare the **model** (names, folder structure, methods, URLs, params, headers, bodies, auth kinds and fields, variables, scoped environments, scripts for Postman) through a test helper
    `ModelSnapshot.Of(collectionTree)` that renders the tree to a canonical text without ids; the expected differences are listed in the test as data (Insomnia: Jwt auth and scripts dropped; Bruno: collection variables become script lines, scripts unchanged; OpenAPI: auth becomes placeholder variables, bodies become examples, only `summary` names survive, path variables become collection variables).
  - **Secrets never exported**: the unlocked-vault secret value does not appear in any exported byte sequence (search every exported file as text), the secret row is present with an empty value and the report carries the `ExportSecretsOmitted` `Info`.
  - **Determinism**: exporting twice gives byte-identical files (zip included).
  - **YAML = JSON**: the OpenAPI YAML export, read with `VYamlTreeReader`, equals the JSON export read with `DataNodeJson` (compact text).
  - Bruno zip entries are the expected paths; no entry escapes the root directory.
  - Exported Postman JSON validates against the structural expectations of v2.1 (`info.schema` text, `item` array, every request has `method` and `url.raw`).
  - Save cancelled: no error, nothing written; the export with a collection deleted between load and export reports `ExportCollectionNotFound`.

## Out of scope

HAR export (blocked), UI tests.

## Acceptance criteria

- [ ] The test class passes locally; no external network.
- [ ] The round-trip tests deferred by the export cards are all present here (list them in the Notes).
- [ ] Architecture tests green, 0 warnings, all tests green.

## Suggested commit message

`test: add end-to-end tests for export and round trips`
