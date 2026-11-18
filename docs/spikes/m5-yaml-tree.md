# Spike M5-02: read arbitrary YAML into a tree under Native AOT

Program: [`docs/spikes/m5-yaml-tree/`](m5-yaml-tree/) (throwaway console project, not in `Yaapic.sln`, own empty `Directory.Build.props`/`Directory.Packages.props`).
VYaml 1.5.0, `net11.0`, `PublishAot=true`, `IsAotCompatible`, `TreatWarningsAsErrors`.

## Verdict

**VYaml's low-level `YamlParser` can build a generic tree under Native AOT.** No fallback reader is needed; M5-07 can be written as planned, with the rules below.
Two API facts the card did not anticipate (both handled by the rules):

- `YamlParser` has **no public scalar style** (plain, quoted, literal, folded) and no public `Marker` on exceptions. Plain-versus-quoted is recovered because `IsNullScalar()`,
  `TryGetScalarAsBool()`, `TryGetScalarAsInt64()` and `TryGetScalarAsDouble()` **succeed on plain scalars only** (`'1.0'`, `"true"`, `'null'` and `"~"` all fail them).
- `SkipHeader()` consumes `StreamStart` **and** the first `DocumentStart` (a following `ReadWithVerify(DocumentStart)` throws). After it the current event is the root node
  (or `StreamEnd` for an empty stream).
- An alias is an `Alias` event whose name is read with `TryGetCurrentAnchor(out var anchor)` (`anchor.Name`), not with `GetScalarAsString()` (null).

## Results

| Sample | Outcome |
|---|---|
| OpenAPI 3.0 with `$ref`, quoted keys `'200'` | tree as expected; `version: '1.0'` is a string, `$ref` a string |
| Scalars `'1.0'` `1.0` `yes` `null` `~` (empty) `"true"` `true` `'null'` | string, number, string, null, null, null, string, bool, string |
| Numbers `12` `'12'` `-0` `1e3` `0.5` `.5` `+1` `0x1F` `1_000` `007` `1.` `.inf` `True` `NO` `Null` | number, string, number, number, number, string, string, string, string, string, string, string, bool, string, null |
| Literal `\|` and folded `>` blocks | strings `line1\nline2\n` and `a b\n` (clip chomping kept) |
| Flow mapping and sequence `{a: 1, b: [x, y]}` | nested map/list as expected |
| Anchor, alias and `<<` merge | `use` = `{y: 3, x: 1}` (own key wins), aliased list copied |
| Leading `---` and trailing `...` | parsed |
| Two documents (`---` between) | first only |
| Comments (full line and trailing) | ignored |
| Windows line endings | parsed, block scalar content normalised to `\n` |
| UTF-8 BOM | parsed |
| Tab-indented error | throws `YamlTokenizerException` (not `YamlParserException`), no crash |
| Unclosed flow sequence | `YamlParserException` ("expected ',' or ']' at Line: 2, Col: 2, Idx: 11") |
| Unknown alias | `YamlParserException` ("found unknown anchor at Line: 2, Col: 0, Idx: 9") |
| 100000-line sequence | 17 ms (AOT), 59 ms (JIT, includes warm-up); limit is 2 s |
| Depth 200 | `FormatException("YAML nested too deeply")` at depth > 64 |
| Billion laughs (9 levels of 10 aliases) | `FormatException("YAML expands to too many nodes")` at 1,000,000 nodes, fast |

## Native AOT publish

`dotnet publish -c Release -r win-x64 -p:PublishAot=true`: **0 warnings** (so 0 IL2xxx/IL3xxx), exe **2.5 MB** (2,588,160 bytes). The published exe ran all 17 samples with
the same output as the JIT run, in the timings above, and exited cleanly.

## Final rules

1. **Scalar typing.** Quoted scalars and block scalars are strings. For a plain scalar: `IsNullScalar()` (`null`, `Null`, `NULL`, `~`, empty) is null;
   `TryGetScalarAsBool()` succeeds and the text is one of `true|false|True|False|TRUE|FALSE` is a bool (`yes`, `no`, `on`, `off` stay strings: YAML 1.2 core);
   `TryGetScalarAsInt64()` or `TryGetScalarAsDouble()` succeeds **and** the text matches `-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?` is a number kept as its text
   (so `.5`, `+1`, `007`, `0x1F`, `1_000`, `1.`, `.inf` stay strings). Everything else is a string. A number whose magnitude overflows `double` is a string; accepted.
2. **Aliases.** Copy the anchored subtree at each alias (anchors are recorded when their node ends, so a self-referencing alias is an unknown-anchor error), count every
   copied node, and throw `FormatException("YAML expands to too many nodes")` above 1,000,000 nodes in total. An unknown alias is a `YamlParserException`.
3. **`<<` merge key: yes.** A plain `<<` key whose value is a mapping, or a sequence of mappings, merges those keys; keys of the current mapping win, earlier merge entries win over later ones.
   A quoted `"<<"` key cannot be told from a plain one (no scalar style), so it is treated as a merge key too; accepted. Non-scalar mapping keys throw `FormatException`.
4. **Multi-document streams: first document only.** After `SkipHeader()`, read one node and ignore the rest. An empty stream gives no tree (null).
5. **Errors.** A syntax error throws `YamlParserException` (parser level) or `YamlTokenizerException` (tokenizer level, for example tabs); both derive from `VYaml.YamlException`,
   so catch `YamlException` and rethrow as `FormatException` in the reader. Neither exposes a public `Marker`: the location is only in `Message`, formatted `... at Line: L, Col: C, Idx: I`
   (the line is 1-based and the position is where the parser stopped, which can be just after the faulty token, e.g. `Line: 2, Col: 0` for an error at the end of line 1). M5-07 shows
   the message as it is. **Depth limit: 64**, `FormatException("YAML nested too deeply")`.

Also record for M5-07: `parser.GetScalarAsString()` may return null for an empty scalar (use `?? ""`), and a block mapping key can be any node, so check it is a scalar.
