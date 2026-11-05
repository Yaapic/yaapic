# Workspace file format v1

Decision records: [ADR 0005](adr/0005-storage-format.md), [ADR 0008](adr/0008-collections-globals-history-storage.md). Serialised with VYaml (`[YamlObject]` types).

## Layout

```
<workspace>/
  .gitignore                      contains ".yaapic/"
  .yaapic/session.yaml            per-user state, never committed
  .yaapic/history.jsonl           per-user send history, never committed
  globals.yaml                    global variables (optional)
  requests/<slug>.request.yaml    unfiled request
  requests/<folder-slug>/         top-level folder = collection
    folder.yaml                   marks the directory as a folder
    <slug>.request.yaml           request in the folder
    <sub-slug>/folder.yaml        nested folder
  environments/<slug>.env.yaml    one environment per file
```

A directory under `requests/` without `folder.yaml` is ignored and reported as a listing error. Siblings are sorted
folders first, then requests, each by name (ordinal, ignoring case), then by id; there is no manual ordering.

## Request file

```yaml
version: 1
id: 6f1c...            # GUID, "D" format
name: Get users
method: GET            # GET | POST | PUT | PATCH | DELETE | HEAD | OPTIONS (upper case)
url: "{{baseUrl}}/users"
params:
  - key: page
    value: "1"
    enabled: true
headers:
  - key: Accept
    value: application/json
    enabled: true
body:
  type: none           # none | raw | urlencoded | form-data | binary
  contentType: json    # text | json | xml | html | javascript
  text: ""
  fields:              # urlencoded and form-data
    - key: avatar
      value: C:\img\a.png   # a path when kind is file
      enabled: true
      kind: text       # text | file
  file: ""             # binary: path of the file to send
auth:
  type: none           # none | basic | bearer | apikey
  username: ""         # basic
  password: ""         # basic
  token: ""            # bearer
  key: ""              # apikey
  value: ""            # apikey
  in: header           # apikey: header | query
settings:
  followRedirects: true
  timeoutSeconds: 0    # 0 = application default (30 s)
scripts:               # optional on read, see ADR 0009
  preRequest:
    language: javascript   # javascript | lua
    source: |
      pm.environment.set("ts", Date.now().toString());
  postResponse:
    language: javascript
    source: ""
```

Key order: `version, id, name, method, url, params, headers, body, auth, settings, scripts`; inside `body`:
`type, contentType, text, fields, file`; inside `scripts`: `preRequest, postResponse`, each with `language, source`.
Keys added in milestone 2 or later (including `scripts`) are optional on read (missing = default: both blocks empty,
`javascript`).

The stored `url` never contains the query of the `params` table. The editor shows the URL with the enabled params
appended and keeps both in sync. A legacy URL that already contains `?a=b` is read as is; the editor moves that inline
query to the front of the params list on load.

## Folder file

Keys, in order: `version: 1`, `id`, `name`, `description`, `variables` (same list shape as `params`), `scripts` (same
shape as in the request file, optional on read; a top-level folder's scripts are the collection scripts). Only the
variables of a top-level folder are used (collection variables); nested folders keep the key for later.

## Globals file

`<workspace>/globals.yaml`: `version: 1`, `variables` (same list shape as `params`). A missing file means no globals.

## Variable precedence

Highest first: active environment, collection variables, globals, dynamic variables.

## History file

`.yaapic/history.jsonl`: one JSON object per line (`System.Text.Json` source generators), append-only, never
committed, not hand-edited:

```json
{"id":"6f1c...","at":"2026-11-02T09:00:00.0000000Z","request":"version: 1\n...","statusCode":200,"statusText":"OK","durationMs":42,"sizeBytes":1024,"failureKind":null,"failureMessage":null}
```

`id` is a GUID ("D"), `at` a UTC ISO 8601 round-trip timestamp, `request` the request as a YAML string written by
`RequestYamlSerializer`, `failureKind` an enum name or null. Response bodies are not stored. Retention: the last 500
entries; the file is rewritten when it exceeds 550 lines. It keeps the request as sent and may hold credentials, which
is why `.yaapic/` is git-ignored. Auth values and variables are stored in clear in the workspace until the secret vault
(M3).

## Environment file

Keys: `version: 1`, `id`, `name`, `variables` (same list shape as `params`).

## Session file

Keys: `version: 1`, `openRequests` (list of GUID strings), `activeRequest` (GUID or empty), `activeEnvironment`
(GUID or empty).

## Rules

- UTF-8 without BOM, `\n` line endings.
- Keys are written in the order shown, and every key is always written (empty lists as `[]`).
- Unknown keys are ignored on read (VYaml's generated formatters skip them, no pre-filtering needed).
- List items are written at the same indentation as their key (`params:` then `- key: a`), which is equivalent YAML.
- Missing optional keys take defaults (`enabled: true`, `body.type: none`).
- A missing or different `version` is an error for request and environment files.
- Multi-line strings are written as literal block scalars (`|`).

## File names (slug)

1. Lowercase the name.
2. Replace every run of characters other than `a-z` and `0-9` with `-`.
3. Trim `-` from both ends.
4. Cut to 60 characters.
5. If the result is empty, use `request` or `environment`.

On collision with another id, append `-2`, `-3`, and so on.
