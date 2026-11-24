# M5-05: Core: DataNode tree, JSON reading and writing

Status: done
Platform: any
Depends on: M5-01

## Goal

A neutral, ordered, loosely typed tree for foreign JSON and YAML documents, with a JSON reader and writer, and the port the YAML reader implements.

## Design decisions

- Read first: ADR 0016 ("Neutral tree", "Security of untrusted files"), `docs/spikes/m5-yaml-tree.md` if present (scalar rules).
- `src/Yaapic.Core/Import/DataNode.cs` (namespace `Yaapic.Core.Import`), `sealed class DataNode`, immutable:
  - `enum DataKind { Null, Bool, Number, String, Array, Object }`; `Kind`.
  - Factories: `static DataNode Null { get; }`, `Bool(bool)`, `Number(string text)` (the number is kept as its source text, never converted, so `1.0` and big integers survive), `String(string)`,
    `Array(IReadOnlyList<DataNode>)`, `Object(IReadOnlyList<KeyValuePair<string, DataNode>>)` (members keep source order, duplicate keys are kept).
  - Readers: `string? AsString()` (String: the text; Number: the source text; Bool: `true` or `false`; other kinds: null), `bool? AsBool()` (Bool; the strings `true` and `false` ignoring case; else null),
    `double? AsDouble()` (Number or a numeric String, invariant culture; else null), `IReadOnlyList<DataNode> Items` (empty unless Array), `IReadOnlyList<KeyValuePair<string, DataNode>> Members` (empty unless Object),
    `DataNode? this[string key]` (last member with that key, ordinal; null when absent or not an Object), `DataNode? this[int index]` (null when out of range or not an Array),
    `string? GetString(string key)` (= `this[key]?.AsString()`), `bool? GetBool(string key)`, `bool IsNull`, `IsObject`, `IsArray`, `IsString`.
  - `string? GetString(params ReadOnlySpan<string> path)` walks nested objects; null when any step is missing.
- `src/Yaapic.Core/Import/DataNodeJson.cs`, `static class DataNodeJson`:
  - `DataNode Parse(string json)`: `JsonDocument.Parse` with `JsonDocumentOptions { MaxDepth = 64, CommentHandling = Skip, AllowTrailingCommas = true }`, converted recursively **without recursion deeper than 64** (use an
    explicit loop or the depth limit of the parser, which already throws). `JsonException` becomes `FormatException(Strings.ImportInvalidJson(message))` (the message carries the line and position of the JsonException).
    Numbers keep `GetRawText()`.
  - `string Write(DataNode node, bool indented = true)`: `Utf8JsonWriter` with `JsonWriterOptions { Indented = indented, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }`, indent 2 spaces; Number written with
    `WriteRawValue(text)` (a Number whose text is not valid JSON number syntax is written as a string); output ends **without** a trailing newline (callers add `\n`).
  - Helper `static DataNode Obj(params (string Key, DataNode Value)[] members)` and `Arr(params DataNode[] items)`, `Str(string)`, `Num(long)` to build trees in exporters and tests.
- `src/Yaapic.Core/Import/IYamlTreeReader.cs`: `interface IYamlTreeReader { DataNode Parse(string yaml); }` (throws `FormatException` with the line and column in the message; implemented in Infrastructure by M5-07).
- `src/Yaapic.Core/Import/StructuredTextReader.cs`: `sealed class StructuredTextReader(IYamlTreeReader yaml)` with `DataNode Parse(string text)`: when the first non-whitespace character is `{` or `[` it parses JSON, otherwise YAML.
- Strings: `import.invalidJson` = "The file is not valid JSON: {0}"; `import.invalidYaml` = "The file is not valid YAML: {0}" (used by M5-07). French by the implementer.

## Out of scope

The YAML reader (M5-07), the YAML writer (M5-08).

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests/Import`: parse an object with nested arrays, numbers `1`, `1.0`, `12345678901234567890` (text preserved), strings with unicode and escapes, `null`, `true`; comments and a trailing comma accepted;
  duplicate keys (last wins in the indexer, both in `Members`); depth 65 throws `FormatException`; invalid JSON throws `FormatException`; the accessor behaviour on wrong kinds (`AsString` of an Array is null, `this[0]` of an Object is null);
  `Write` round trip of a tree (indented output compared with an exact expected string, compact output too, a number such as `1.50` stays `1.50`); non-ASCII characters are not escaped; `StructuredTextReader` picks JSON for `{` and `[`,
  and calls a fake `IYamlTreeReader` otherwise.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the DataNode tree and JSON reading and writing`
