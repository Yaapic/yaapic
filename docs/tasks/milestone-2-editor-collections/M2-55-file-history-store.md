# M2-55: File history store (JSON Lines)

Status: done
Platform: any
Depends on: M2-54, M2-08

## Goal

`IHistoryStore` on `<workspace>/.yaapic/history.jsonl` (ADR 0008).

## Design decisions

- `internal sealed class HistoryLineDto` and `[JsonSerializable(typeof(HistoryLineDto))] internal partial class
  HistoryJsonContext : JsonSerializerContext` with `[JsonSourceGenerationOptions(PropertyNamingPolicy =
  JsonKnownNamingPolicy.CamelCase, WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.Never)]`, so every key
  is written (nulls included). Members as in ADR 0008: `id` (string "D"), `at` (string, `DateTimeOffset.ToString("O")`),
  `request` (YAML string from `RequestYamlSerializer`), `statusCode` (int?), `statusText`, `durationMs` (long),
  `sizeBytes` (long), `failureKind` (string? enum name), `failureMessage` (string?). Source-generated JSON only, no
  reflection (add a note to `docs/dependencies.md`: `System.Text.Json` is part of the framework, no new package).
- `public sealed class FileHistoryStore(string workspaceRoot) : IHistoryStore`, file
  `Path.Combine(workspaceRoot, ".yaapic", "history.jsonl")`, UTF-8 without BOM, `\n` line endings. A `SemaphoreSlim`
  serializes every operation of an instance.
  - `AppendAsync`: creates the directory when missing, appends one line; when the line count then exceeds 550, rewrites
    the file atomically (`WorkspaceFiles.WriteAtomicAsync`) with the newest 500 lines. Track the count in memory after the
    first read to avoid re-reading on every append.
  - `ListAsync`: missing file -> empty; a line that fails to parse (JSON or the embedded YAML) is skipped, never throws;
    result newest first, capped at 500.
  - `DeleteAsync` and `ClearAsync`: rewrite atomically (clear deletes the file).
  - I/O exceptions -> `StorageException`.

## Out of scope

Searching in the file, response bodies, export.

## Acceptance criteria

- [x] Tests in `Yaapic.Infrastructure.Tests` on a temp directory: append and list round trip of a success and a failure
      entry (request equivalence, status, duration, size, failure members); newest first; a corrupt line in the middle is
      skipped; 551 appends leave 500 lines and the newest 500 entries; delete and clear; missing directory and file;
      concurrent appends (e.g. 20 `Task`s) lose nothing and never interleave a line; no `.tmp` left; no BOM.
- [x] AOT analyzers clean, 0 warnings.

## Suggested commit message

`feat(infrastructure): add file history store`
