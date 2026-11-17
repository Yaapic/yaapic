# M5-11: Infrastructure: import input reader (file and folder)

Status: todo
Platform: any
Depends on: M5-10

## Goal

`FileImportInputReader` reads a chosen file or folder into an `ImportInput`, enforcing the size limits.

## Design decisions

- Read first: ADR 0016 (limits and "Security of untrusted files"), M5-10 (`IImportInputReader`), `src/Yaapic.Infrastructure/Storage/FileRequestStore.cs` (how files are read: UTF-8, error mapping).
- `src/Yaapic.Infrastructure/Import/FileImportInputReader.cs`, `public sealed class FileImportInputReader : IImportInputReader`.
  - `ReadFileAsync`: the file must exist (`ImportException(Strings.ImportFileNotFound(path))`) and be at most 50 MiB (`Strings.ImportFileTooLarge(path)`); text decoded as UTF-8, a BOM stripped, invalid bytes replaced (no exception);
    result `ImportInput(Path.GetFileNameWithoutExtension(path), [new ImportFile(Path.GetFileName(path), text)])`.
  - `ReadFolderAsync`: the folder must exist; enumerate recursively in a deterministic order (relative path, ordinal, ignoring case), keeping only files with the extensions `.bru`, `.json`, `.yml`, `.yaml`; skipping directories named
    `node_modules`, `.git` and any directory starting with `.`; symbolic links and reparse points are skipped (not followed); at most 5000 files and 200 MiB of text in total (`Strings.ImportFolderTooLarge`), each file at most 50 MiB (a bigger one is skipped, not fatal);
    relative paths use `/`; no file readable gives `ImportException(Strings.ImportFolderEmpty(path))`. `Name` is the folder name.
  - `IOException` and `UnauthorizedAccessException` on one file in a folder skip that file; on `ReadFileAsync` they become `ImportException(Strings.ImportFileUnreadable(path, message))`. Cancellation is checked per file.
- Strings (EN and FR): `import.fileNotFound` = "File not found: {0}"; `import.fileTooLarge` = "{0} is larger than 50 MB and cannot be imported."; `import.folderTooLarge` = "This folder has more than 5000 files or 200 MB of data."; `import.folderEmpty` = "No importable file in {0}.";
  `import.fileUnreadable` = "Cannot read {0}: {1}".

## Out of scope

Reading from a URL, zip files, drag and drop.

## Acceptance criteria

- [ ] Tests in `Yaapic.Infrastructure.Tests/Import/FileImportInputReaderTests` on a temporary directory: a UTF-8 file with BOM, a missing file, an oversize file (use a sparse or generated 51 MiB file and delete it), folder listing order and relative `/` paths, filtering by
  extension, skipped `.git` and `node_modules`, more than 5000 files (generate 5001 empty `.bru` files) throws, an empty folder throws, a file with invalid UTF-8 bytes does not throw, cancellation. A test for the reparse point skip is added only if the OS allows creating a symbolic link without privileges (skip otherwise).
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(infrastructure): add the import input reader`
