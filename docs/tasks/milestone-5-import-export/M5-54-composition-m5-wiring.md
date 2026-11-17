# M5-54: Composition: wire the M5 services

Status: todo
Platform: any
Depends on: M5-07, M5-08, M5-09, M5-10, M5-11, M5-13, M5-14, M5-15, M5-16, M5-18, M5-22, M5-23, M5-24, M5-26, M5-29, M5-34, M5-35, M5-37, M5-40, M5-41, M5-43, M5-44, M5-46, M5-53

## Goal

`AppComposition.CreateShell` builds the importers, exporters and view model factories and attaches them to the shell, as M4-61 did for the milestone 4 services.

## Design decisions

- Read first: M4-61 and `src/Yaapic.Composition/AppComposition.cs` (how `shell.AttachTools` and the palette are wired, the secret-aware stores, `ISecretVault` and `VaultViewModel` state for "vault unlocked"), `docs/architecture.md`, ADR 0016.
- In `AppComposition` (the internal `CreateShell` overload that takes every dependency, so tests can substitute): build once and share
  - `var yaml = new VYamlTreeReader(); var structured = new StructuredTextReader(yaml);`
  - importers in this order (registration order is the tie-break of `Detect`): `new PostmanCollectionImporter()`, `new PostmanEnvironmentImporter()`, `new OpenApiImporter(structured)`, `new InsomniaImporter()`, `new HarImporter()`, `new BrunoImporter()`, `new CurlImporter()` -> `ImporterRegistry`.
  - `ImportApplier(requestStore, collectionStore, environmentStore, globalsStore, () => vault.State == VaultState.Unlocked)` using the **secret-aware decorated stores** that the shell already uses (`SecretAwareCollectionStore`, `SecretAwareEnvironmentStore`, `SecretAwareGlobalsStore`; the request store as in the shell), `ImportService`, `FileImportInputReader`.
  - exporters in this order: `PostmanCollectionExporter`, `PostmanEnvironmentExporter`, `OpenApiJsonExporter`, `OpenApiYamlExporter`, `InsomniaExporter`, `BrunoExporter` -> `ExporterRegistry`; `ExportSourceReader(collectionStore, environmentStore, globalsStore)` on the same decorated stores (so secret values come blank-able: the reader blanks them anyway).
  - `shell.AttachImportExport(() => new ImportViewModel(importService, importerRegistry, reader, dialogs, shell.ReloadAsync), () => new ExportViewModel(exporterRegistry, exportSourceReader, collectionStore, environmentStore, dialogs))` (a new `ImportViewModel` and `ExportViewModel` per dialog opening).
- `docs/dependencies.md` is not touched (no new package). `docs/architecture.md` is updated by M5-67.
- A test project reference is **not** added to any App project.

## Out of scope

Views, docs.

## Acceptance criteria

- [ ] A test in `Yaapic.Infrastructure.Tests` (where the shell composition is tested, as for M4-61) that a shell built on a temporary workspace: lists the seven importers and six exporters in order; `CreateImport()` previews a small Postman collection text pasted through `PreviewPastedCommand`, imports it, the collection tree contains the new folder and requests (`Collections.RefreshAsync` ran);
  `CreateExport()` exports that collection as Postman JSON to the `FakeDialogService` `SavedFiles` and the bytes parse back with `DataNodeJson.Parse`; the palette lists the Import and Export entries.
- [ ] A test that the secrets policy is wired: with a locked vault a Postman environment with a secret row imports with a blank value and the report `Warning`; with an unlocked vault the value reaches the vault (use the existing test vault helpers of the milestone 3 end-to-end tests).
- [ ] AOT publish of `Yaapic.App.WinUI` (or the host-RID app that M4-61 used): 0 IL2xxx/IL3xxx warnings; the exe starts (kill it afterwards).
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(composition): wire the import and export services`
