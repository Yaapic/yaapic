# M5-08: Core: DataNode YAML writer

Status: done
Platform: any
Depends on: M5-05

## Goal

`DataNodeYaml.Write` serializes a `DataNode` to YAML for the OpenAPI YAML export, deterministically and without a library.

## Design decisions

- Read first: M5-05 (`DataNode`, `DataNodeJson`).
- `src/Yaapic.Core/Import/DataNodeYaml.cs`, `public static class DataNodeYaml`, `string Write(DataNode node)`; output uses `\n`, 2-space indentation, ends with a single `\n`.
- Rules (final):
  - Object: one `key: value` line per member in order; a member whose value is a non-empty Object or Array puts the value on the following lines indented by 2 (`key:` then children); an **empty** Object is `{}`, an empty Array `[]` on the same line.
  - Array: each item on its own line starting with `- `; an item that is a non-empty Object writes its first member after `- ` and the other members aligned under it (indent + 2); an item that is a non-empty Array writes `-` then its items indented by 2;
    scalar items after `- `.
  - Keys: written bare when they match `^[A-Za-z_][A-Za-z0-9_-]*$`, otherwise as a JSON-style double-quoted string. Examples that must stay readable: `/pets/{id}` is quoted (`"/pets/{id}"`), `200` is quoted (`"200"`), `x-token` is bare.
  - Scalars: Null `null`; Bool `true`/`false`; Number its source text; String **always double-quoted** with JSON escaping (`\"`, `\\`, `\n`, `\r`, `\t`, `\uXXXX` for control characters; non-ASCII written as is). No block scalars, no anchors, no tags.
- The output must be readable by `VYamlTreeReader` (M5-07) and by any YAML 1.2 parser; the round-trip test lives in `Yaapic.Infrastructure.Tests` (this card has no VYaml access).

## Out of scope

Comments, flow style, line folding.

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests/Import/DataNodeYamlTests` with exact expected texts written by hand: a nested object with arrays of objects and arrays of arrays, empty `{}` and `[]`, keys needing quotes, strings with quotes, backslashes, newline,
  tab, unicode, a string that looks like a number or `true` (still quoted), numbers `1.50` and `-3`, null, root scalar, root array, trailing newline.
- [x] The round trip through `VYamlTreeReader` (M5-07) is checked by the OpenAPI YAML export test of M5-43 and by M5-66, not here (this card does not depend on M5-07).
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the DataNode YAML writer`
