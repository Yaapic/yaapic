# M5-22: Core: OpenAPI import, request bodies

Status: done
Platform: any
Depends on: M5-20, M5-21

## Goal

`requestBody` of an operation becomes a Yaapic `RequestBody` (plus the `Content-Type` header when needed), using the example or the sampled schema.

## Design decisions

- Read first: M5-20, M5-21, M5-13 (the body shapes), `src/Yaapic.Domain/Http/RequestBody.cs`.
- `src/Yaapic.Core/Import/OpenApi/OpenApiBodies.cs` (internal static): `RequestBody Map(DataNode? requestBody, OpenApiRefResolver refs, OpenApiSchemaSampler sampler, string path, ImportReport report)`; called by `OpenApiOperations` per operation (`requestBody` resolved through `refs`; absent or Null -> `RequestBody.None`).
- **Media type choice** among `content` keys, first match in this order: exactly `application/json`; a key ending `+json`; `application/x-www-form-urlencoded`; `multipart/form-data`; `application/xml` or a key ending `+xml`; a key starting `text/`; otherwise the first key. Parameters after `;` are ignored when matching.
- **Sample value** of the chosen media object: `example`; else the first of `examples` (its `value`, resolved); else `sampler.Sample(schema)`.
- Mapping by chosen media type:

  | Media type | Result |
  |---|---|
  | JSON (`application/json`, `*+json`) | `Raw Json`, text = `DataNodeJson.Write(sample, indented: true)`; a sample that is a String node (an example given as text) is used as the raw text unchanged |
  | XML (`application/xml`, `*+xml`, `text/xml`) | `Raw Xml`; the sample when it is a string, otherwise (sampled object) text `<!-- example body not generated for XML schemas -->` and `Info` `Strings.ImportOpenApiXmlSample(path)` |
  | `text/html`, `text/javascript`, `application/javascript`, other `text/*` | `Raw` with `ContentTypeFromMime`; text = the sample as a string, `""` when it is not a string |
  | `application/x-www-form-urlencoded` | `UrlEncoded`: when the sample is an object, one field per member (`value` = scalar as text, objects and arrays as compact JSON); otherwise one field per `schema.properties` entry with an empty value |
  | `multipart/form-data` | `FormData`: one field per sample member (or schema property); a property whose schema has `format: binary` (or `type: string, contentEncoding`) is a `File` field with an empty path and `Warning` `Strings.ImportOpenApiFileField(path, name)`; others `Text` |
  | `application/octet-stream` and other binary types | `Binary` with an empty `FilePath` and `Warning` `Strings.ImportOpenApiBinaryBody(path)` |

- **Content-Type header**: when the chosen media type is not what the body kind sends by itself (`UrlEncoded`, `FormData` and the raw kinds Json, Xml, Html, JavaScript, Text send their own type; but an exotic type such as `application/vnd.api+json` or `application/merge-patch+json`
  or `text/csv` would be lost) the operation gets an explicit header `Content-Type: <media type>` (enabled). Rule: add the header when the media type, lower case, is not one of `application/json`, `application/xml`, `text/xml`, `text/html`, `text/plain`, `application/javascript`, `text/javascript`, `application/x-www-form-urlencoded`, `multipart/form-data`.
  The return value is `(RequestBody Body, string? ContentTypeHeader)`.
- `requestBody.required` is ignored. Several media types: only the chosen one is imported, and one `Info` `Strings.ImportOpenApiOtherMediaTypes(path, count)` is added when `content` had more than one key.
- Strings (EN and FR): `import.openapi.xmlSample` = "{0}: no XML example could be generated from the schema."; `import.openapi.fileField` = "{0}: the file field \"{1}\" has no path; choose a file before sending."; `import.openapi.binaryBody` = "{0}: the binary body has no file; choose a file before sending.";
  `import.openapi.otherMediaTypes` = "{0}: {1} other body formats were not imported."

## Out of scope

Security, response bodies, `encoding` objects of multipart.

## Acceptance criteria

- [x] Tests: media type choice order with several keys; JSON from `example`, from `examples`, from a schema, from a string example; urlencoded from sample and from properties only; multipart with a binary property; octet-stream; XML example string and XML object schema (the placeholder + `Info`);
  `application/vnd.api+json` adds the header while `application/json` does not; `required` ignored; no `requestBody` gives `None`; `$ref` request body from `components/requestBodies`; the other-media-types `Info`.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): import OpenAPI request bodies`
