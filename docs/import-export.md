# Importing and exporting

Yaapic imports collections, environments and requests from other tools and exports them back. Decisions:
[ADR 0016](adr/0016-import-export-architecture.md) (architecture, limits, apply policy) and
[ADR 0017](adr/0017-import-export-formats.md) (versions, mappings, losses). Code layout: [architecture.md](architecture.md).

Open **Import...** from the sidebar or the command palette, and **Export...** from the palette or the context menu of a
top-level folder.

## Import

### Steps

1. Choose the source: a file, a folder (Bruno) or pasted text.
2. Yaapic detects the format; you can override it in the format list.
3. Read the preview: counts (requests, folders, environments, globals) and the report, which can be filtered by severity.
   **Nothing has been written yet.**
4. Press **Import**. Yaapic writes the collection, environments and globals through the usual stores.

### Supported formats

| Format | What is read |
|---|---|
| Postman | Collection v2.0 and v2.1 (also the `{"collection": ...}` wrapper); environment and globals files |
| OpenAPI | 3.0.x and 3.1.x, JSON or YAML; Swagger 2.0 (normalized to 3.0 first); local `$ref` only |
| cURL | One or more `curl` commands in bash syntax, including the browser "Copy as cURL (bash)" form |
| HAR | 1.2 (1.1 files have the same shape) |
| Insomnia | v4 JSON export (`__export_format` 3 or 4) |
| Bruno | A collection folder: `bruno.json`, `.bru` files and `environments/` |

Not supported: Insomnia 5 YAML, Postman v1, OpenCollection YAML (Bruno 3), WSDL/SOAP, GraphQL schemas, gRPC protos.

### How it is mapped

- The collection becomes a new top-level folder named after the source (Postman `info.name`, OpenAPI `info.title`, the
  Insomnia workspace, Bruno `bruno.json`, the HAR file name). A name already in use becomes `Name (2)`, `Name (3)`
  (reported as `Info`); environments are numbered the same way.
- Folders and directories become nested folders. OpenAPI operations go in one folder per first tag.
- Siblings are sorted by name, so the source ordering (Postman item order, Bruno `seq`, Insomnia sort keys) is lost; this is
  reported once as `Info`.
- cURL and HAR requests are named `METHOD host/path` (80 characters at most) and are not filed in a collection.
- `{{name}}` is kept (Postman, Bruno). Insomnia `{{ _.name }}` becomes `{{name}}`. An OpenAPI path parameter `{id}` becomes
  `{{id}}` with a collection variable; the first OpenAPI server gives the collection variable `baseUrl`, the next ones one
  environment each.
- Collection variables of Postman and Bruno and the Insomnia base environment become the variables of the new folder.
  Insomnia sub-environments and Bruno environments become environments scoped to the collection. Postman environment files
  become environments, Postman globals files become globals (only keys not yet present are added; existing values win).
- Authentication inherited from a parent is copied onto each request that has none (nearest ancestor wins), reported once.
- Dynamic variables: `$guid`, `$timestamp`, `$isoTimestamp` and `$randomInt` exist; `$randomUUID` becomes `$guid`; any other
  `{{$name}}` is kept as text with a warning.

### What is lost and how it is reported

Every loss is a report entry; nothing is dropped silently.

| Dropped or altered | Level |
|---|---|
| Descriptions of requests, saved response examples, tests written as UI assertions | dropped, reported |
| Bruno `assert` blocks | Warning, dropped |
| Authentication kinds without a Yaapic equivalent (NTLM, Hawk, OAuth 1, Akamai, WSSE, openIdConnect, OAuth 2 implicit) | Warning, request imported without auth |
| Cookies and cookie jars | Info |
| Proxy and TLS settings (`curl -k`, `--cert`, `--proxy`) | Warning |
| Insomnia template tags other than environment references, `uuid` and `now` | Warning, kept as text |
| Postman dynamic variables outside the supported ones | Warning, kept as text |
| Path variables (`:id`) | substituted or turned into `{{id}}` variables |
| Scripts that use APIs outside `yaapic` | Warning per finding |
| Source ordering of siblings | Info, once per import |
| Methods other than GET, POST, PUT, PATCH, DELETE, HEAD, OPTIONS | Skipped |

Severities: **Info** (converted, or a note), **Warning** (converted with a loss or a risk), **Skipped** (not imported). The
path of each entry points at the place in the source (`Folder/Sub/Request`, `paths./pets.get`, `entries[3]`). A request or
folder that fails to be saved is `Skipped` and the rest continues; there is no rollback, and deleting the new top-level folder
undoes the requests.

### Secrets and the vault

Rows the importer marks as secret (for example Postman `secret` variables and Bruno `vars:secret`) are stored through the
secret vault **only when it is unlocked**. Otherwise their value is dropped, the secret flag is cleared and one warning says
how many values must be entered again. Nested folders cannot hold secrets, so their secret rows never keep a value.
**Imported credentials that are not marked secret (passwords, tokens, API keys in auth blocks, headers) are stored as plain
text** in the workspace files, like any other value you type: review them, and move them to the vault if needed.

### File references and scripts

- A multipart file, a binary body or a cURL `@file` is imported as a path and gets a warning naming it: the file is read and
  **sent** when the request is sent. Check these before sending.
- `$ref` to a file or a URL is never followed (reported). Yaapic never fetches a URL while importing.
- Postman and Bruno scripts are converted to JavaScript with `ScriptConverter` (see
  [scripting-compatibility.md](scripting-compatibility.md)); each finding is a warning with the script member and line, and
  one more warning says the collection contains scripts to review before sending. Insomnia scripts are not converted and are
  reported.

### Limits

50 MiB per file; for a folder 5000 files and 200 MiB; JSON and YAML depth 64; YAML aliases may expand to 1,000,000 nodes;
20000 requests per import. Text is decoded as UTF-8.

## Export

| Format | Written | Fidelity |
|---|---|---|
| Postman | Collection v2.1 (`.json`); environment file | read back by the Postman importer without changes except ids; secret values blanked |
| OpenAPI | 3.0.3, JSON or YAML | **inferred from requests** (no responses are stored), so best effort and lossy; says so as an `Info` |
| Insomnia | v4 JSON | read back exactly, except scripts (written unchanged with a warning, never converted back on import) |
| Bruno | A `.zip` of a collection folder | read back exactly, except Jwt auth (warned) |

Rules shared by every exporter: output is deterministic (UTF-8 without BOM, `\n`, stable key order, no timestamps); one file
is saved as is and several are packaged as a `.zip`; **secret values are never exported** (the vault is not read, the value
of every secret row is blanked and the report says how many). Postman exports rewrite `yaapic.` to `pm.` in scripts; Bruno and
Insomnia get the script text unchanged with a warning. Bruno has no collection-level equivalent of globals (not exported,
`Info`); Bruno **collection variables** are written as `vars:pre-request` pairs in `collection.bru` (enabled rows only), and
the Bruno importer reads them back as `yaapic.variables.set` lines in the collection script.

cURL export is code generation ("Generate code", see [response-tools.md](response-tools.md)).

## Open questions (blocked cards)

- **Insomnia 5 YAML import** (M5-30).
- **HAR export** (M5-47).
- **Pasting a cURL command into the URL box** (M5-48). Import cURL from the import dialog instead.

## Troubleshooting

| Message | Cause and fix |
|---|---|
| The format of this file was not recognized. Choose the format manually. | Detection found no importer. Pick the format in the list; if it still fails, the file is not one of the supported versions. |
| The import is too large (request count) | More than 20000 requests, or a file or folder over the limits above. Split the source. |
| Invalid JSON ... / Invalid YAML ... | The file is malformed or nested too deeply; the message gives the position. Fix or re-export it. |
| Secret values must be entered again | The vault was locked during the import. Unlock it and import again, or fill the values in by hand. |
