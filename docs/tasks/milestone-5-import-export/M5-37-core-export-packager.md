# M5-37: Core: export packager (zip)

Status: done
Platform: any
Depends on: M5-36

## Goal

Several export files become one `.zip` the user saves as a single file; one file is saved as is.

## Design decisions

- Read first: M5-36 (`ExportFile`, `ExportResult`), ADR 0016 ("Export contract").
- `src/Yaapic.Core/Export/ExportPackager.cs`, `public static class ExportPackager`:
  - `(string FileName, byte[] Content) Package(ExportResult result, string baseName, string extension)`: exactly one file -> `(<that file's Path>, its Content)`; more than one -> `(<baseName>.zip, zip bytes)`; no files -> `InvalidOperationException`.
    `baseName` is made file-system safe with the ADR 0005 slug rule (`docs/file-format.md`, "File names": lowercase, runs of other characters to `-`, trimmed, 60 characters, empty -> `export`).
  - The zip is built with `System.IO.Compression.ZipArchive` in memory (`CompressionLevel.Optimal`); entry names are the `ExportFile.Path` values (`/` separators); **deterministic**: `LastWriteTime` of every entry set to `2000-01-01T00:00:00Z` (`DateTimeOffset`), entries in the order given.
    A path that is rooted, contains `..` or a backslash is refused with `ArgumentException` (zip-slip guard on our own output).
  - `string SafeFileName(string name)`: the slug rule above, used by the exporters for file names, with a `Func`-free helper `UniquePath(string path, ISet<string> used)` that appends `-2`, `-3` before the extension on a clash (ignoring case).
- Strings: none.

## Out of scope

Reading zips, other archive formats.

## Acceptance criteria

- [x] Tests: single file returned as is with its path as the name; three files give a zip (read it back with `ZipArchive`: names, contents, order, the fixed timestamp); two calls give byte-identical zips; a path with `..`, a rooted path and a backslash are refused; no file throws;
  `SafeFileName("My API (v2)!")` = `my-api-v2`, empty -> `export`, 80 characters cut to 60; `UniquePath` clash handling ignoring case.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the export packager`
