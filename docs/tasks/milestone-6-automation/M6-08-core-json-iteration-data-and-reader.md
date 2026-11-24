# M6-08: Core: JSON iteration data and the data file reader

Status: done
Platform: any
Depends on: M6-07

## Goal

Parse JSON data files and offer one entry point that reads either format from the file text (ADR 0018).

## Design decisions

- Read first: ADR 0018, M6-07, `src/Yaapic.Core/Import/DataNode.cs` and `DataNodeJson.cs` (the M5 JSON tree reader, depth limit).
- `src/Yaapic.Core/Runner/JsonIterationData.cs`: `public static IReadOnlyList<IterationDataRow> Parse(string text)`, through
  `DataNodeJson.Parse` (reuse it, do not write a second JSON reader). The root must be an array, else
  `IterationDataException(Strings.RunnerDataJsonNotArray)`; each element must be an object, else `Strings.RunnerDataJsonNotObject(index)`
  (0-based). Values: string as is; number and boolean as their JSON text (`DataNode` keeps numbers as text); `null` as `""`; object
  and array as compact JSON (`DataNodeJson.Write(node, indented: false)`). Key order kept. Invalid JSON: the parser's
  message wrapped in `Strings.RunnerDataInvalidJson(message)`. Limits as in M6-07 (rows, columns = keys of one object).
- `src/Yaapic.Core/Runner/IterationDataReader.cs`:
  `public static IterationData Read(string fileName, string text)` returning
  `public sealed record IterationData(string FileName, IReadOnlyList<IterationDataRow> Rows, IReadOnlyList<string> Columns)`
  (`FileName` = `Path.GetFileName(fileName)`; `Columns` = names in first-seen order over all rows). The text is already decoded:
  the view models read the file with the existing `IImportInputReader.ReadFileAsync` (UTF-8, BOM removed, M5 size limit). A text
  longer than `IterationDataLimits.MaxBytes` characters throws `RunnerDataTooLarge`; a leading U+FEFF is removed. Format by extension
  (case-insensitive `.csv`, `.json`), otherwise JSON when the first non-whitespace character is `[`, otherwise CSV.
- Strings (EN and FR): `runner.dataJsonNotArray` = "A JSON data file must contain an array of objects.";
  `runner.dataJsonNotObject` = "Item {0} of the data file is not an object."; `runner.dataInvalidJson` = "Invalid JSON: {0}".

## Out of scope

Picking the file (M6-20), the runner.

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests/Runner/`: JSON rows with every value kind; key order; not an array; an element not an object;
  invalid JSON; `IterationDataReader` picks CSV or JSON by extension and by content; a leading U+FEFF removed; over 10 MiB refused;
  `Columns` union in first-seen order.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): read JSON and CSV iteration data files`
