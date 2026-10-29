# ADR 0005: Storage format

Status: Accepted

## Context

Collections, environments and requests are stored as files in a workspace folder, diffed and merged by Git, and often
hand-edited. The format must be readable, give clean diffs and be serialised without reflection.

## Options

1. **YAML with VYaml.** Human friendly; multi-line strings (scripts, bodies) stay readable in diffs; comments allowed.
2. **JSON with `System.Text.Json` source generators.** No extra dependency and proven AOT, but no comments and escaped
   multi-line strings make diffs hard to read.
3. **Custom text format.** Full control, but a parser to build and no ecosystem.

## AOT risks

VYaml (MIT) documents support for Native AOT and trimming: its source generator emits, per assembly, a module
initializer that statically registers formatters for `[YamlObject]` partial types, enums, collections and closed
generics, with no reflection on that path. Caveats to respect: root generic shapes (for example `List<T>`) need
`[assembly: YamlSerializable(typeof(...))]`; polymorphism uses `[YamlObjectUnion]`; never use `dynamic` or
`Type.GetType` round-trips. This check is based on the library's documentation: the first card that adds VYaml must
still run the AOT publish and confirm 0 IL2xxx/IL3xxx warnings. Fallback if it fails: option 2.

## Decision

Option 1: YAML via VYaml, with models marked `[YamlObject]`. JSON is the rejected option for storage (it stays in use
for import/export formats, through `System.Text.Json` source generators).
