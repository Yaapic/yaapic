# M5-24: Core: Swagger 2.0 normalization

Status: done
Platform: any
Depends on: M5-19

## Goal

A Swagger 2.0 document is rewritten into an equivalent OpenAPI 3.0 `DataNode` before the shared import code reads it, so only one importer path exists.

## Design decisions

- Read first: M5-19 (`OpenApiImporter`, the `swagger: "2.0"` branch), the OpenAPI 3.0 migration guide as a reference for the rules below.
- `src/Yaapic.Core/Import/OpenApi/SwaggerNormalizer.cs`, `internal static class SwaggerNormalizer`, `DataNode ToOpenApi3(DataNode swagger, ImportReport report)`; `OpenApiImporter.Import` calls it when the root has `swagger` equal to `2.0` and then continues with the result as if it were an OpenAPI 3.0 document. `Info` once: `Strings.ImportOpenApiSwaggerConverted`.
- Rules (the result keeps the member order given here):
  - `openapi: "3.0.3"`, `info` copied, `servers`: for each `scheme` in `schemes` (default `["https"]` when `host` is present; none when `host` is absent): `{url: "<scheme>://<host><basePath>"}` (basePath default empty, no trailing `/`); no `host`: one server `{url: "<basePath>"}` when basePath is not empty. `tags` copied.
  - `paths`: each operation copied with its members except `parameters`, `consumes`, `produces`, `responses`, `schemes`, `security` (copied as is). Parameters (path-item level and operation level, resolved against `#/parameters`): `in: body` becomes `requestBody` (`content` of every media type in the operation's `consumes`, else
    the document's `consumes`, else `["application/json"]`, each `{schema: <param.schema>}`, `required` copied); `in: formData` parameters are collected into one `requestBody`: media type `multipart/form-data` when any has `type: file` or the consumes list has it, else `application/x-www-form-urlencoded`; schema `{type: object, properties: {name: {type, format, ...}}}`, a `type: file` parameter becomes `{type: string, format: binary}`;
    other parameters (`query`, `header`, `path`) become `{name, in, required, description, schema: {type, format, enum, default, items}}` (the Swagger fields `type`, `format`, `enum`, `default`, `items`, `example`-less moved under `schema`; `collectionFormat` ignored). Path-item level `parameters` stay on the path item.
  - `definitions` -> `components.schemas` (the same nodes), `parameters` and `responses` are not carried over (parameters are already inlined; refs into `#/parameters/...` are resolved by the normalizer when it inlines). Every `$ref` string starting `#/definitions/` is rewritten to `#/components/schemas/` throughout the tree.
  - `securityDefinitions` -> `components.securitySchemes`: `type: basic` -> `{type: http, scheme: basic}`; `type: apiKey` unchanged; `type: oauth2`: `flow` `application` -> `flows.clientCredentials {tokenUrl, scopes}`, `password` -> `flows.password {tokenUrl, scopes}`, `accessCode` -> `flows.authorizationCode {authorizationUrl, tokenUrl, scopes}`, `implicit` -> `flows.implicit {authorizationUrl, scopes}`. Top-level `security` copied.
  - `responses`, `produces`, examples are dropped (never imported anyway).
- Nodes are immutable: build new `DataNode`s (a small recursive `Rewrite` helper that copies objects and arrays and rewrites ref strings).
- Strings (EN and FR): `import.openapi.swaggerConverted` = "Swagger 2.0 document converted to OpenAPI 3 before import."

## Out of scope

OpenAPI 3.1 specific handling, Swagger 1.2.

## Acceptance criteria

- [x] Tests: a Swagger 2 document (inline JSON) converted and compared with the expected compact JSON for: servers from host + basePath + two schemes, no host with basePath, a body parameter with consumes, formData without and with a file, query/header/path params moved under `schema`, path-item level parameters, `$ref` to `#/parameters/...` inlined,
  `definitions` renamed and refs rewritten, the four oauth2 flows, basic and apiKey, top-level `security`.
- [x] End-to-end through the importer (with M5-20 done at that point or a fake): a Swagger 2 document imports to the same requests as its 3.0 equivalent (compare names, URLs, parameters and body kinds). If M5-20, M5-22 and M5-23 are not all `done`, test only the normalizer output and say so in the Notes.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): convert Swagger 2.0 documents to OpenAPI 3 before import`
