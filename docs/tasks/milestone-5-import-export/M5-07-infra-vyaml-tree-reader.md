# M5-07: Infrastructure: YAML tree reader (VYaml)

Status: done
Platform: any
Depends on: M5-02, M5-05

## Goal

`VYamlTreeReader` implements `IYamlTreeReader` and turns any YAML text into a `DataNode`.

## Design decisions

- Read first: `docs/spikes/m5-yaml-tree.md` (the five final rules and the AOT result; **if the spike concluded that VYaml's low-level parser does not work under AOT, stop and report to the user**), M5-05 (`DataNode`, `IYamlTreeReader`),
  `src/Yaapic.Infrastructure/FolderYamlSerializer.cs` (how VYaml is used there).
- `src/Yaapic.Infrastructure/Import/VYamlTreeReader.cs`, `public sealed class VYamlTreeReader : IYamlTreeReader`, built on VYaml's `YamlParser` events exactly as the spike did. Rules: the ones recorded in the spike (scalar typing,
  alias expansion limit, `<<` merge, first document only, depth limit 64 -> `FormatException(Strings.ImportInvalidYaml("nested too deeply"))`). A `YamlParserException` becomes
  `FormatException(Strings.ImportInvalidYaml(<message with line and column>))`. A leading BOM is ignored. Input text is encoded to UTF-8 bytes once.
- No reflection, no `YamlSerializer` generic deserialization. No new package.

## Out of scope

Writing YAML (M5-08), format-specific logic.

## Acceptance criteria

- [x] Tests in `Yaapic.Infrastructure.Tests/Import/VYamlTreeReaderTests`, one per spike sample: plain scalar typing table (`1`, `1.0`, `'1.0'`, `true`, `"true"`, `null`, `~`, empty, `yes` stays the string `yes`, `3.0.1` stays a string), literal and folded blocks,
  flow collections, anchor and alias (copies), merge key, `---` and `...`, comments, CRLF, BOM, tab error throws `FormatException` naming a line, depth 65 throws, alias bomb (nested aliases beyond the node cap) throws `FormatException`, a small OpenAPI document
  read and navigated through `DataNode` (`paths./pets.get.summary`), the first of two documents only.
- [x] A test that `StructuredTextReader` (M5-05) with this reader parses the same document given as JSON and as YAML into trees whose `DataNodeJson.Write(compact)` are equal.
- [x] AOT publish of `Yaapic.App.Avalonia` (or the host RID app that is quickest): 0 new IL2xxx/IL3xxx warnings (the reader is not wired yet, so also run the existing Infrastructure AOT check if the repository has one; otherwise state it in the Notes).
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(infrastructure): add the YAML tree reader`

## Notes

- CRLF is normalised to LF before parsing (VYaml kept CR inside block scalars). Alias copies share the immutable anchored node; only their size is charged against the 1,000,000-node cap.
- AOT: `Yaapic.App.Avalonia` publishes with 0 IL2xxx/IL3xxx warnings; the reader is not wired yet and there is no separate Infrastructure AOT check in the repository.
