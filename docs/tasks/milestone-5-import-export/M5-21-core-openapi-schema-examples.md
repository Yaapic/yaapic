# M5-21: Core: OpenAPI schema example generator

Status: todo
Platform: any
Depends on: M5-19

## Goal

When an operation has a request body schema but no example, generate a sample value from the schema, deterministically.

## Design decisions

- Read first: M5-19 (`OpenApiRefResolver`, `DataNode`), M5-05 (`DataNodeJson.Obj`/`Arr`/`Str`/`Num`).
- `src/Yaapic.Core/Import/OpenApi/OpenApiSchemaSampler.cs`, `internal sealed class OpenApiSchemaSampler(OpenApiRefResolver refs)`, `DataNode Sample(DataNode schema, string path)`; the result is a `DataNode` that `DataNodeJson.Write` pretty-prints later.
- Rules in order (the first that applies wins; `schema` is resolved through `refs.Resolve` first; Null/absent schema gives `DataNode.Null`):
  1. `example` -> that value. 2. `default` -> that value. 3. `enum` -> its first value. 4. `const` (3.1) -> its value.
  5. `allOf`: sample every member that is an object and merge their properties in order (a later key replaces an earlier one); non-object members are ignored. 6. `oneOf` or `anyOf`: the sample of the first member.
  7. `type` (3.1 arrays of types: the first that is not `"null"`; no `type` but `properties` -> object; no `type` but `items` -> array):
     - `object`: one member per entry of `properties` in order, skipping properties with `readOnly: true`; no properties -> `{}`. `additionalProperties` ignored.
     - `array`: one element, the sample of `items` (no `items` -> `[]`).
     - `string`: by `format`: `date-time` `"2024-01-01T00:00:00Z"`, `date` `"2024-01-01"`, `time` `"00:00:00"`, `uuid` `"00000000-0000-0000-0000-000000000000"`, `email` `"user@example.com"`, `uri` and `url` `"https://example.com"`, `hostname` `"example.com"`, `ipv4` `"192.0.2.1"`, `ipv6` `"2001:db8::1"`, `byte` `""`, `binary` `""`, `password` `"password"`; no format: `"string"`; when `minLength` is greater than 6, the text is `"string"` padded on the right with `x` to `minLength` (cap 256).
     - `integer`: `minimum` when present (as a number text), else `0`. `number`: the same, `0` default. `boolean`: `false`. `null`: null.
  8. Nothing matched: `DataNode.Null`.
- **Limits**: depth limit 6 levels of nested objects and arrays below the root (deeper schemas give `DataNode.Null` at the limit); a schema already being expanded (recursive refs) gives `DataNode.Null` there; the total node count is capped at 2000 (then `DataNode.Null`).
  Every cut adds one `Info` per import `Strings.ImportOpenApiSampleTruncated` (use `AddOnce`).
- Strings (EN and FR): `import.openapi.sampleTruncated` = "Some example bodies were cut because the schema is recursive or very large."

## Out of scope

Choosing a body or content type (M5-22), `pattern` regex generation, `xml` object naming, `discriminator`.

## Acceptance criteria

- [ ] Tests: each rule with an exact `DataNodeJson.Write(compact)` expectation: example/default/enum/const priority; objects with nested arrays; `readOnly` omitted; every string format; `minLength` padding; integer `minimum`; allOf merge with a replaced key; oneOf; 3.1 `type: ["string","null"]`; schema through `$ref`;
  a self-referencing schema stops with `null` and adds the `Info` once; depth 7 cut; the node cap on a wide array of objects schema; missing schema.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): generate example values from OpenAPI schemas`
