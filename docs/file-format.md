# Workspace file format v1

Decision record: [ADR 0005](adr/0005-storage-format.md). Serialised with VYaml (`[YamlObject]` types).

## Layout

```
<workspace>/
  .gitignore                      contains ".yaapic/"
  .yaapic/session.yaml            per-user state, never committed
  requests/<slug>.request.yaml    one request per file
  environments/<slug>.env.yaml    one environment per file
```

## Request file

```yaml
version: 1
id: 6f1c...            # GUID, "D" format
name: Get users
method: GET            # GET | POST (upper case)
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
  type: none           # none | raw
  contentType: json    # text | json | xml
  text: ""
```

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
