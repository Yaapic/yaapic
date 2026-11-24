# ADR 0017: Import and export formats, mappings and losses

Status: Accepted

See also: [ADR 0016](0016-import-export-architecture.md), [ADR 0008](0008-collections-globals-history-storage.md),
[ADR 0005](0005-storage-format.md), [file format](../file-format.md), [YAML spike](../spikes/m5-yaml-tree.md).
The format cards of milestone 5 hold the field-level tables; this ADR is the summary a user and a reviewer read.

## Context

ADR 0016 fixes how import and export work. This ADR fixes what is read and written: the versions of each format, the
high-level mapping to the Yaapic model, and what is lost and at which level it is reported.

## Decision

### Supported versions

| Format | Import | Export |
|---|---|---|
| Postman | Collection v2.0 and v2.1 (JSON, also the `{"collection": ...}` wrapper of the API); environment and globals files | Collection v2.1; environment file |
| OpenAPI | 3.0.x and 3.1.x, JSON or YAML; Swagger 2.0 (normalized to 3.0 first); local `$ref` only | 3.0.3, JSON or YAML (inferred from requests, lossy) |
| cURL | One or more `curl` commands in bash syntax (including the browser "Copy as cURL (bash)" form) | none here: code generation (M4-51) |
| HAR | 1.2 (and 1.1 files, same shape) | blocked (M5-47) |
| Insomnia | v4 JSON export (`__export_format` 3 or 4) | v4 JSON |
| Bruno | A collection folder (`bruno.json` and `.bru` files, environments) | A `.zip` of the same layout |

Not supported: Insomnia 5 YAML (blocked, M5-30), Postman v1, OpenCollection YAML of Bruno 3, WSDL/SOAP, GraphQL
schemas, gRPC protos.

### Names and structure

- The collection name is: Postman `info.name`, OpenAPI `info.title`, Insomnia the workspace name, Bruno `bruno.json`
  `name`, HAR the file name without extension.
- Postman and Insomnia folders and Bruno directories become nested folders. OpenAPI operations are grouped in one
  folder per first tag; operations without a tag sit at the collection root.
- Yaapic sorts siblings by name (ADR 0008): the source ordering (Postman item order, Bruno `seq`, Insomnia sort keys)
  is lost and reported once as `Info`.
- cURL and HAR requests are named `METHOD host/path` (no scheme, no query, 80 characters at most). Other formats keep
  the source name; an empty name becomes `METHOD path`.

### Variables

- `{{name}}` is the same syntax in Postman and Bruno and is copied as is. Insomnia `{{ _.name }}` becomes `{{name}}`.
- An OpenAPI path parameter `{id}` becomes `{{id}}` with a collection variable `id`.
- OpenAPI servers give the collection variable `baseUrl`; the second and later servers give one environment each.
- Collection variables of Postman and Bruno and the Insomnia base environment become the collection folder's
  variables. Insomnia sub-environments and Bruno environments become environments scoped to the collection
  (`CollectionId`). Postman environment files become unscoped environments; Postman globals files become globals.

### Loss table

| Dropped or altered | Level |
|---|---|
| Descriptions of requests, saved response examples, tests written as UI assertions | dropped, reported |
| Bruno `assert` blocks | Warning, dropped |
| Authentication kinds without a Yaapic equivalent (NTLM, Hawk, OAuth 1, Akamai, WSSE, openIdConnect, OAuth 2 implicit) | Warning, request imported without auth |
| Cookies and cookie jars | Info |
| Proxy and TLS settings (`curl -k`, `--cert`, `--proxy`) | Warning |
| Insomnia template tags other than environment references, `uuid` and `now` | Warning, kept as text |
| Postman dynamic variables outside the four supported | Warning, kept as text |
| Path variables (`:id`) | substituted or turned into `{{id}}` variables |
| Scripts that use APIs outside `yaapic` | Warning per finding from `ScriptConverter` |
| Source ordering of siblings | Info, once per import |

### Export fidelity

Exports are meant to be read back by the importer of the same format: a round-trip test per format (M5-66) checks
that the Yaapic model is unchanged except for ids. The OpenAPI export is a best-effort description inferred from
requests (no responses are stored) and says so in the report as an `Info`.

### Open product questions

Three cards are blocked on a product decision and nothing depends on them: M5-30 (Insomnia 5 YAML), M5-47 (HAR
export) and M5-48 (pasting a cURL command into the URL box).

## Consequences

Users get a predictable table of what is read, written and lost; every loss is a report entry at the level above
rather than a silent drop. New formats or versions require a new ADR or an amendment of this one.
