# M6-07: Core: CSV iteration data parser

Status: done
Platform: any
Depends on: M6-01

## Goal

Parse a CSV data file into iteration rows (ADR 0018, "Iteration data").

## Design decisions

- Read first: ADR 0018, RFC 4180, `src/Yaapic.Domain/Http/KeyValueEntry.cs`, `src/Yaapic.Core/Import/ImportHelpers.cs` (how
  UTF-8 with BOM is decoded in M5).
- New folder `src/Yaapic.Core/Runner/`.
  - `IterationDataRow.cs`: `public sealed record IterationDataRow(IReadOnlyList<KeyValueEntry> Values)` with `static IterationDataRow Empty`
    and `bool TryGet(string name, out string value)` (first match, ordinal).
  - `IterationDataException.cs`: `public sealed class IterationDataException(string message) : Exception(message)` (localized message).
  - `CsvIterationData.cs`: `public static class CsvIterationData { public static IReadOnlyList<IterationDataRow> Parse(string text) }`.
- Rules: the first record is the header (column names trimmed; an empty name becomes `column{n}` with n 1-based; a duplicate name
  keeps its first column and later ones are named `{name}_{n}`); separator `,`; a field may be quoted with `"`, `""` inside quotes is
  one `"`, quoted fields may contain `,`, CR and LF; unquoted fields are taken as is (no trimming); line ends CRLF, LF or CR; a final
  empty line is ignored; blank lines in the middle are skipped; a record shorter than the header gets `""` for the missing columns;
  a record longer than the header throws `IterationDataException(Strings.RunnerDataTooManyFields(line))`; a quote that is never closed
  throws `Strings.RunnerDataUnclosedQuote(line)` (line numbers are 1-based physical lines). Text must already be decoded (BOM removed
  by the caller, M6-08).
- Limits (constants on a new `public static class IterationDataLimits`: `MaxBytes = 10 * 1024 * 1024`, `MaxRows = 100_000`,
  `MaxColumns = 1_000`): more columns or rows throw `Strings.RunnerDataTooLarge`.
- A header with no data record returns an empty list.
- Strings (EN and FR): `runner.dataTooManyFields` = "Line {0} has more fields than the header.";
  `runner.dataUnclosedQuote` = "Line {0} has a quote that is never closed."; `runner.dataTooLarge` =
  "The data file is too large (10 MiB, 100000 rows and 1000 columns at most).".
- No regex; a single pass over the characters.

## Out of scope

JSON (M6-08), reading the file, the runner.

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests/Runner/CsvIterationDataTests.cs`: simple file; quoted commas, quotes and multi-line fields; CRLF,
  LF and CR; trailing newline; blank middle lines; short and long records; unclosed quote with its line number; empty and duplicate
  header names; header only; 1001 columns refused; 100001 rows refused; Unicode text kept.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): parse CSV iteration data`
