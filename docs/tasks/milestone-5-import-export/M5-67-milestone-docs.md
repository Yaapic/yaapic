# M5-67: Milestone 5 docs

Status: todo
Platform: any
Depends on: M5-65, M5-66, M5-57, M5-58, M5-59, M5-62, M5-63, M5-64

## Goal

The documentation matches what milestone 5 delivered, and the task index records the milestone summary.

## Design decisions

- Read first: M4-80 (the same card for milestone 4) and the "Milestone 4 summary" block of `docs/tasks/README.md`, ADRs 0016 and 0017, `docs/spikes/m5-yaml-tree.md`, the Notes sections of the M5 cards.
- `docs/architecture.md`: a "Import and export (milestone 5)" section with a small component diagram in the existing style: `IImportInputReader` -> `ImportService` (`ImporterRegistry`, the seven importers, `DataNode`, `StructuredTextReader`/`IYamlTreeReader`) -> `ImportBundle`/`ImportReport` -> `ImportApplier` -> the secret-aware stores; the export side: `ExportSourceReader` -> `IExporter`s -> `ExportPackager` -> `IDialogService.SaveFileAsync`; the view models and the shell entries.
- `docs/file-format.md`: a short note that imports write ordinary workspace files (no new key), that imported credentials are plain text, and that secret values are never exported.
- `docs/dependencies.md`: Milestone 5: **no new package** (System.Text.Json, System.IO.Compression, regex source generation from the framework; VYaml already used; the YAML writer is our own code).
- A new `docs/import-export.md` (user level): for each format what is read (versions), how it is mapped, what is lost and how it is reported (copy the loss table of ADR 0017, per format), the import dialog steps, the report severities, secrets and the vault, file references and scripts review warnings, exports and their fidelity, the OpenAPI export being inferred, what Bruno collection variables become,
  Insomnia 5 and HAR export and paste-cURL being open questions, troubleshooting messages (not recognized, too large, invalid JSON/YAML).
- `docs/scripting-compatibility.md`: one line saying the importers reuse `ScriptConverter` and that exports rewrite `yaapic.` to `pm.` for Postman only.
- `README.md` (repository root): the feature list gains import and export.
- `docs/tasks/README.md`: add the "Milestone 5 summary" block after the card table (delivered, deferred, open items, runtime checks not run) in the style of the Milestone 4 summary; set the M5 row of the milestone table to `detailed (68 cards, see above)` if it is not already.

## Out of scope

Screenshots (M5-68).

## Acceptance criteria

- [ ] The documents above are updated and their relative links work (open each).
- [ ] The summary lists the blocked cards still open (M5-30, M5-47, M5-48) and anything deferred (undo of an import, import from a URL, drag and drop, history export, Postman cloud, gRPC/GraphQL/WebSocket items).
- [ ] No code changed; the build still has 0 warnings.

## Suggested commit message

`docs: document the milestone 5 features`
