# M5-43: Core: OpenAPI export, bodies, security and the JSON and YAML exporters

Status: done
Platform: any
Depends on: M5-42, M5-08

## Goal

Complete the OpenAPI document (request bodies with inferred schemas, security schemes) and expose it as two exporters.

## Design decisions

- Read first: M5-42, M5-08 (`DataNodeYaml`), M5-22 and M5-23 (the import side), `src/Yaapic.Domain/Http/RequestBody.cs`, `RequestAuth.cs`.
- `src/Yaapic.Core/Export/OpenApi/OpenApiBodyWriter.cs` (internal static): `(DataNode RequestBody)? Write(RequestBody body)`; added to each operation as `requestBody` (`{ "content": { "<media type>": { "schema": ..., "example": ... } } }`); no body kind `None` -> no member.
  - `Raw`: media type by content type: Json `application/json`, Xml `application/xml`, Html `text/html`, JavaScript `application/javascript`, Text `text/plain`. For **Json**: if the text parses with `DataNodeJson.Parse` the `example` is the parsed value and the `schema` is inferred by `InferSchema`; if it does not parse (it holds `{{variables}}` in unquoted places) the example is the text as a string and the schema `{ "type": "string" }`. Other raw kinds: `schema { "type": "string" }`, `example` = the text.
  - `UrlEncoded`: `application/x-www-form-urlencoded`, schema `{ "type": "object", "properties": { <field>: { "type": "string" } } }` (enabled fields only), `example` an object of field -> value.
  - `FormData`: `multipart/form-data`, properties as above but a `File` field is `{ "type": "string", "format": "binary" }`; `example` omitted.
  - `Binary`: `application/octet-stream`, schema `{ "type": "string", "format": "binary" }`.
  - `InferSchema(DataNode)`: Object -> `{ "type": "object", "properties": { member: InferSchema(value) } }` (member order kept); Array -> `{ "type": "array", "items": InferSchema(first item) }` (empty array: `{ "type": "array", "items": {} }`); String -> `{ "type": "string" }`; Number -> `integer` when its text has no `.`, `e` or `E`, else `number`;
    Bool -> `boolean`; Null -> `{ "nullable": true }`. Depth limit 8 (deeper values give `{}`).
- `src/Yaapic.Core/Export/OpenApi/OpenApiSecurityWriter.cs` (internal static): collects `components.securitySchemes` and returns the `security` requirement per request. A request auth maps to a scheme name and definition (the same auth kind with the same `ApiKey` name/location or the same OAuth 2 URLs reuses one scheme; names are unique):

  | Auth | scheme name | definition |
  |---|---|---|
  | Basic | `basicAuth` | `{ "type": "http", "scheme": "basic" }` |
  | Bearer | `bearerAuth` | `{ "type": "http", "scheme": "bearer" }` |
  | Jwt | `bearerAuth` (same as Bearer when both exist) | `{ "type": "http", "scheme": "bearer", "bearerFormat": "JWT" }` (when Bearer also exists the JWT one is named `jwtAuth`) |
  | Digest | `digestAuth` | `{ "type": "http", "scheme": "digest" }` |
  | ApiKey | `apiKeyAuth`, `apiKeyAuth2`, ... | `{ "type": "apiKey", "name": key, "in": "header"|"query" }` |
  | OAuth2 | `oauth2Auth`, `oauth2Auth2`, ... | `{ "type": "oauth2", "flows": { "clientCredentials"|"authorizationCode"|"password": { "tokenUrl", "authorizationUrl" (authorizationCode only), "scopes": { <each word of Scope>: "" } } } }` |
  | AwsV4 | `awsSigV4` | `{ "type": "apiKey", "name": "Authorization", "in": "header", "description": "AWS Signature Version 4" }` |

  Each operation with auth gets `"security": [ { "<scheme name>": [ <scope words for OAuth2, else none> ] } ]`; the document gets `components` `{ "securitySchemes": {...} }` only when at least one scheme exists. Member order of the document: `openapi, info, servers, tags, paths, components`.
- `src/Yaapic.Core/Export/OpenApi/OpenApiJsonExporter.cs` and `OpenApiYamlExporter.cs` (`public sealed class ...Exporter : IExporter`): ids `openapi-json` and `openapi-yaml`, display names `OpenAPI 3.0 (JSON)` and `OpenAPI 3.0 (YAML)`, extensions `.json` and `.yaml`, `RequiresCollection` true. File name `SafeFileName(source.Name) + ".openapi" + extension`.
  Content: `DataNodeJson.Write(doc, true) + "\n"` or `DataNodeYaml.Write(doc)` (it already ends with `\n`), UTF-8 without BOM. Both build the document once with `OpenApiDocumentBuilder.Build`.
- Strings: none new.

## Out of scope

Response schemas, `components/schemas` deduplication, examples of several media types.

## Acceptance criteria

- [x] Tests: a collection with a JSON body (valid, and with `{{var}}` making it invalid), urlencoded, multipart with a file, binary, XML text; `InferSchema` for nested objects, arrays, ints, decimals, booleans, null, depth limit; each security row; two ApiKey auths with different names get `apiKeyAuth` and `apiKeyAuth2`; Bearer plus Jwt naming;
  OAuth2 scopes in the requirement; no auth means no `components`; JSON and YAML exporters produce the same document (read the YAML back with `VYamlTreeReader` in `Yaapic.Infrastructure.Tests` and compare the compact JSON; if M5-07 is not `done`, leave that comparison to M5-66 and say so in the Notes); file names, extensions, no BOM.
- [x] Round trip with `OpenApiImporter` (M5-20, M5-22, M5-23): export then import preserves methods, names (via summary), paths (`{{baseUrl}}` appears as the server variable) and body kinds; if those cards are not `done`, leave it to M5-66.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): export collections as OpenAPI 3.0 in JSON and YAML`
