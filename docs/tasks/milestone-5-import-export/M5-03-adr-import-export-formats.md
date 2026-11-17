# M5-03: ADR 0017: supported formats, mappings and losses

Status: todo
Platform: any
Depends on: M5-01, M5-02

## Goal

Write ADR 0017: which versions of which formats milestone 5 reads and writes, the high-level mapping to the Yaapic model, and what is lost and how it is reported. The format cards hold the field-level
tables; the ADR is the summary a user and a reviewer read. Docs only. The decisions below are final.

## Design decisions (to record in the ADR)

- Read first: ADR 0016 (M5-01), `docs/file-format.md`, the spike `docs/spikes/m5-yaml-tree.md`.
- **Supported versions.**

  | Format | Import | Export |
  |---|---|---|
  | Postman | Collection v2.0 and v2.1 (JSON, also the `{"collection": ...}` wrapper of the API); environment and globals files | Collection v2.1; environment file |
  | OpenAPI | 3.0.x and 3.1.x, JSON or YAML; Swagger 2.0 (normalized to 3.0 first); local `$ref` only | 3.0.3, JSON or YAML (inferred from requests, lossy) |
  | cURL | One or more `curl` commands in bash syntax (including the browser "Copy as cURL (bash)" form) | none here: code generation (M4-51) |
  | HAR | 1.2 (and 1.1 files, same shape) | blocked (M5-47) |
  | Insomnia | v4 JSON export (`__export_format` 3 or 4) | v4 JSON |
  | Bruno | A collection folder (`bruno.json` and `.bru` files, environments) | A `.zip` of the same layout |

  Not supported: Insomnia 5 YAML (blocked M5-30), Postman v1, OpenCollection YAML of Bruno 3, WSDL/SOAP, GraphQL schemas, gRPC protos.
- **Names and structure.** The collection name is: Postman `info.name`, OpenAPI `info.title`, Insomnia the workspace name, Bruno `bruno.json` `name`, HAR the file name without extension.
  Postman and Insomnia folders and Bruno directories become nested folders; OpenAPI operations are grouped in one folder per first tag; operations without a tag sit at the collection root.
  Yaapic sorts siblings by name (ADR 0008): source ordering (Postman item order, Bruno `seq`, Insomnia sort keys) is lost and reported once as `Info`.
- **Loss table** (what is dropped, level): descriptions of requests, saved response examples, tests written as UI assertions, Bruno `assert` blocks (Warning, dropped), authentication
  kinds without a Yaapic equivalent (NTLM, Hawk, OAuth 1, Akamai, WSSE, openIdConnect, OAuth 2 implicit; Warning, request imported without auth), cookies and cookie jars (Info), proxy and TLS
  settings (Warning for `curl -k`, `--cert`, `--proxy`), Insomnia template tags other than environment references, `uuid` and `now` (Warning, kept as text), Postman dynamic variables outside the
  four supported (Warning, kept as text), path variables (`:id`) are substituted or turned into `{{id}}` variables, scripts that use APIs outside `yaapic` (Warning per finding from `ScriptConverter`).
- **Variables.** `{{name}}` is the same syntax in Postman and Bruno (copied as is); Insomnia `{{ _.name }}` becomes `{{name}}`; OpenAPI path `{id}` becomes `{{id}}` with a collection variable `id`; OpenAPI servers
  give the collection variable `baseUrl` and, for a second and later server, one environment each. Collection variables of Postman and Bruno and the Insomnia base environment become the collection
  folder's variables; Insomnia sub-environments and Bruno environments become environments scoped to the collection (`CollectionId`); Postman environment files become unscoped environments,
  Postman globals files become globals.
- **Request naming.** cURL and HAR requests are named `METHOD host/path` (no scheme, no query, 80 characters at most); other formats keep the source name (empty: `METHOD path`).
- **Export fidelity.** Exports are meant to be read back by the importer of the same format (a round-trip test per format in M5-66 checks the Yaapic model is unchanged except for ids). OpenAPI export is a
  best-effort description inferred from requests (no responses are stored), stated in the report as an `Info`.
- **Open product questions** (blocked cards, no card depends on them): M5-30 Insomnia 5 YAML, M5-47 HAR export, M5-48 pasting a cURL command into the URL box.

## Out of scope

Code, UI, field-level tables (they are in the format cards).

## Acceptance criteria

- [ ] `docs/adr/0017-import-export-formats.md` contains the table and every decision above (status Accepted); the ADR index lists it.
- [ ] No code changed; the build still has 0 warnings.

## Suggested commit message

`docs(adr): add ADR 0017 import and export formats`
