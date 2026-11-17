# M5-46: Core: Bruno export, collection layout, environments and the exporter

Status: todo
Platform: any
Depends on: M5-45, M5-37

## Goal

`BrunoExporter` writes a whole Bruno collection folder (as files for the packager) from a Yaapic collection.

## Design decisions

- Read first: M5-33 (the layout read by the importer), M5-35 (environments), M5-45, M5-37 (`ExportPackager.SafeFileName`, `UniquePath`).
- `src/Yaapic.Core/Export/Bruno/BrunoExporter.cs`, `public sealed class BrunoExporter : IExporter`, `Id` `bruno`, `DisplayName` `Bruno collection (.zip)`, `FileExtension` `.zip`, `RequiresCollection` true. It returns **several** `ExportFile`s (the VM packages them with `ExportPackager.Package(result, source.Name, ".zip")`).
  A collection with no request still yields `bruno.json` (so a zip is produced).
- Paths (all under a root directory named `SafeFileName(collection name)`): `<root>/bruno.json` = `{ "version": "1", "name": <collection name>, "type": "collection", "ignore": ["node_modules", ".git"] }` (`DataNodeJson.Write(indented)` + `\n`);
  `<root>/collection.bru` only when the collection folder has scripts (`script:pre-request`, `script:post-response` with the same comment line and indentation as M5-45) — Bruno has no collection-level equivalents of the other Yaapic fields;
  folders: `<root>/<folder slug>/folder.bru` with `meta { name: <folder name> / seq: <position, 1-based> }` plus the folder's scripts; requests: `<dir>/<request slug>.bru` through `BruWriter.WriteRequest(request, seq)` where `seq` is the 1-based position among siblings (folders and requests numbered separately).
  Slugs: `SafeFileName(name)`, made unique **per directory** with `UniquePath` (ignoring case); `folder.bru` and `bruno.json` are reserved names (a request slug equal to `folder` becomes `folder-2`).
- **Environments**: `<root>/environments/<SafeFileName(name)>.bru` for each exported environment (unique names), content `vars {` the enabled **non-secret** rows `key: value` (disabled rows with `~`) `}` then, when any secret rows exist, `vars:secret [` one name per line, comma separated (`token,` ... the last without comma) `]`. A request-scoped Yaapic environment is exported as is; unscoped ones too.
  Globals are not exported (`source.Globals` ignored; Bruno has no equivalent) and `Info` `Strings.ExportBrunoGlobalsIgnored` when it was non-empty. Collection variables (`Variables` of the collection folder): written into `collection.bru` as `vars:pre-request` pairs (the Bruno equivalent of M5-34's reading) — enabled rows only; this makes `collection.bru` exist whenever variables or scripts exist.
- The warnings and infos of `BruWriter` (scripts, binary body, Jwt) go to the export report.
- Strings (EN and FR): `export.bruno.globalsIgnored` = "Global variables were not exported: Bruno has no equivalent."

## Out of scope

Importing zips, OpenCollection YAML.

## Acceptance criteria

- [ ] Tests: the file list and paths of a nested collection (exact list, in order: `bruno.json`, `collection.bru`, folders depth first with `folder.bru` before their requests, then `environments/*`); slug uniqueness (two requests named `Get users`, a request named `folder`); `bruno.json` content; `collection.bru` with variables and scripts;
  environments with secret names list; globals ignored with the `Info`; seq numbering; an empty collection yields `bruno.json` only; packaging with `ExportPackager.Package` gives a zip whose entries equal the paths.
- [ ] Round trip with `BrunoImporter` (M5-33 to M5-35): export, build an `ImportInput` from the files (relative paths without the root directory), import: equal names, methods, URLs, params, headers, bodies, auth (except Jwt), folder structure, environments and secret names; collection variables come back as script lines (documented in M5-34), so compare them as `yaapic.variables.set` calls; if the import cards are not `done`, leave it to M5-66.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): export collections as a Bruno collection folder`
