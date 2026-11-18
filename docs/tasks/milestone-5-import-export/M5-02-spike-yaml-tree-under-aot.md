# M5-02: Spike: read arbitrary YAML into a tree under Native AOT

Status: done
Platform: any
Depends on: none

## Goal

OpenAPI files are often YAML; the workspace only uses VYaml's source-generated `[YamlObject]` types. Prove that VYaml's low-level `YamlParser` (events) can build a
generic tree (`DataNode`, M5-05) under Native AOT with 0 IL2xxx/IL3xxx warnings, and write down the exact conversion rules M5-07 will implement.

## Design decisions

- Read first: `docs/dependencies.md` (VYaml section), M1-10 notes, `src/Yaapic.Infrastructure/FolderYamlSerializer.cs`.
- Throwaway program under `docs/spikes/m5-yaml-tree/` (a console project **not** in `Yaapic.sln`, copy the AOT settings of the existing M4 spike or of `Yaapic.Infrastructure`), referencing VYaml
  1.5.0. It parses the samples below with `YamlParser` (`YamlParser.FromBytes`, `Read`, `CurrentEventType`, `TryGetScalarAsString`/`GetScalarAsString`, `ParseEventType.*`,
  anchors through `TryGetCurrentAnchor`/`ParseEventType.Alias`), builds a plain tree of `Dictionary`/`List`/strings in the program, prints it, and is published with `PublishAot=true`.
- Samples (each must parse and print the expected tree): a small OpenAPI 3.0 YAML with `$ref` strings, quoted and plain scalars (`'1.0'`, `1.0`, `yes`, `null`, `~`, empty value, `"true"`),
  a multi-line literal block (`|`) and folded block (`>`), a flow mapping and flow sequence (`{a: 1, b: [x, y]}`), an anchor with an alias and a `<<` merge key, a document with a leading `---`
  and a trailing `...`, comments, Windows line endings, a document with a BOM, a tab-indented error (must throw `YamlParserException`, not crash), a 100000-line sequence (parses in
  under 2 seconds), and a deeply nested document (depth 200: decide the depth limit, the plan is 64 and `FormatException("YAML nested too deeply")`).
- Decide and record (the later cards depend on these): (1) scalar typing rules for plain scalars (the plan: `true|false|True|False|TRUE|FALSE` bool; `null|Null|NULL|~` and empty null; text
  matching `-?(0|[1-9][0-9]*)(\.[0-9]+)?([eE][+-]?[0-9]+)?` number kept as its text; everything else, and every quoted scalar, string); (2) alias expansion (copy the anchored subtree, cap the total
  expanded node count at 1,000,000 to stop "billion laughs"); (3) `<<` merge key support (yes or no; the plan: yes, keys of the current mapping win); (4) multi-document streams (read the first document only);
  (5) what `YamlParserException` carries for the line and column of the error message.
- Output: `docs/spikes/m5-yaml-tree.md` with the results table (sample, outcome), the AOT publish result (warnings, exe size, a run of the published exe on the samples), and the final rules (1) to (5).
  **If the low-level parser cannot do this under AOT**, record that, state the smallest alternative (a hand-written YAML subset reader in Core, listing the supported subset) and add it to the notes: the
  parent session then rewrites M5-07.

## Out of scope

Writing YAML (M5-08), Yaapic code, the real `VYamlTreeReader` (M5-07).

## Acceptance criteria

- [x] The spike program and `docs/spikes/m5-yaml-tree.md` committed; the document lists the AOT publish result and the five decisions.
- [x] `Yaapic.sln` unchanged (the spike is not referenced), build still 0 warnings; no process left running.

## Suggested commit message

`docs(spike): read arbitrary YAML into a tree under Native AOT`
