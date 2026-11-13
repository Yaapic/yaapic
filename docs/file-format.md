# Workspace file format v1

Decision records: [ADR 0005](adr/0005-storage-format.md), [ADR 0008](adr/0008-collections-globals-history-storage.md). Serialised with VYaml (`[YamlObject]` types).

## Layout

```
<workspace>/
  .gitignore                      contains ".yaapic/"
  .yaapic/session.yaml            per-user state, never committed
  .yaapic/history.jsonl           per-user send history, never committed
  .yaapic/workspace.id            workspace GUID (names the user's vault file), never committed
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
variables of a top-level folder are used (collection variables); nested folders keep the key for later. Scripts exist on every folder level.

A script `source` that starts with a space or a tab is written as a double-quoted scalar (the YAML library emits an
unparsable literal block for it); this applies to request and folder files alike, and both forms read back identically.

## Globals file

`<workspace>/globals.yaml`: `version: 1`, `variables` (same list shape as `params`). A missing file means no globals.

## Secret variables

A variable row of an environment, a folder or the globals file may carry `secret: true` (default `false`, omitted when
false). The row then keeps `value: ""` in the YAML: the real value lives in the per-user vault
(`<user data dir>/Yaapic/vaults/<workspaceId>.vault`, outside the workspace, never in Git), see
[ADR 0010](adr/0010-secret-vault.md). `.yaapic/workspace.id` holds the workspace GUID (one line, "D" format), created on
first use. A fresh clone has no vault, so its secret variables are unresolved until the values are entered again.

```yaml
variables:
  - key: token
    value: ""
    secret: true
```

Only variable lists carry the flag: it is ignored on read and never written for request params, headers or form fields.
Auth fields (`password`, `token`, API key value) are not secret-aware: they stay plain text. Write `{{name}}` in the
field and define `name` as a secret variable instead.

## Vault file (per user, not part of the workspace)

`<user data dir>/Yaapic/vaults/<workspaceId>.vault` (`%APPDATA%` on Windows, `~/Library/Application Support` on macOS,
`$XDG_DATA_HOME` or `~/.local/share` on Linux). It is written by the application only, never hand-edited and never
committed; the file is replaced atomically (temp file, then move) and is `0600` on Unix. It is indented JSON
(`System.Text.Json` source generators, camelCase keys), see [ADR 0010](adr/0010-secret-vault.md):

```json
{
  "version": 1,
  "protector": "passphrase",
  "wrappedKey": "<base64>",
  "kdf": { "algorithm": "pbkdf2-sha512", "iterations": 600000, "salt": "<base64>" },
  "entries": [
    { "kind": "Environment", "owner": "6f1c...", "name": "token", "nonce": "<base64>", "ciphertext": "<base64>", "tag": "<base64>" }
  ]
}
```

`protector` is `passphrase`, `dpapi`, `keychain` or `secret-service`; `kdf` is `null` unless the protector is
`passphrase`. `kind` is `Environment`, `Collection` (top-level folder id) or `Globals` (owner is the empty GUID).
A different `version` is refused.

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
is why `.yaapic/` is git-ignored. Requests keep their `{{templates}}`, so the value of a secret variable is never
stored; an auth value or other field typed literally is stored in clear (see "Secret variables").

## Environment file

Keys: `version: 1`, `id`, `name`, `collection` (optional), `variables` (same list shape as `params`).

`collection` is the id of a collection (top-level folder). An environment with a `collection` is offered only on requests
of that collection; without it (the key is omitted) it is offered everywhere. A collection that no longer exists is ignored.
A request outside any collection is offered the environments without a `collection`.

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
