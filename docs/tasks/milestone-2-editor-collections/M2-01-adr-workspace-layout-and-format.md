# M2-01: ADR 0008 and file format extensions

Status: done
Platform: any
Depends on: none

## Goal

Record every storage decision of milestone 2 once, in docs, so the code cards that follow only implement them.

## Design decisions

Write `docs/adr/0008-collections-globals-history-storage.md` (status Accepted, same sections as ADR 0005: Context,
Options, AOT risks, Decision) and update `docs/file-format.md` and the ADR index. The decisions are final:

- **Collections are directories.** `requests/` stays the root. A directory under `requests/` that contains a
  `folder.yaml` is a folder; a top-level folder is a *collection* (the UI shows another icon, the file format does not
  differ). Requests directly in `requests/` are *unfiled* and keep working unchanged (no migration). A directory
  without `folder.yaml` is ignored and reported as a listing error.
- **Names on disk** follow the existing slug rule (directory name = slug of the folder name, `-2`, `-3` on collision
  among siblings). Renaming a folder or request renames its directory/file.
- **Order**: siblings are sorted folders first, then requests, each by name (ordinal, ignoring case), then by id.
  There is no manual ordering and no drag and drop in M2.
- **Format version stays 1.** New keys are optional on read (missing = default) and always written. Unknown keys are
  still ignored. The request file gains: `method` values `PUT | PATCH | DELETE | HEAD | OPTIONS`; `body.type` values
  `urlencoded | form-data | binary`; `body.contentType` values `html | javascript`; `body.fields` (list of `key`,
  `value`, `enabled`, `kind: text | file`; for `file`, `value` is a path); `body.file` (binary path); `auth`
  (`type: none | basic | bearer | apikey`, `username`, `password`, `token`, `key`, `value`, `in: header | query`);
  `settings` (`followRedirects: true`, `timeoutSeconds: 0`, where 0 means the application default of 30 s). Key order:
  `version, id, name, method, url, params, headers, body, auth, settings`; inside `body`: `type, contentType, text,
  fields, file`.
- **Folder file** `folder.yaml`: `version: 1`, `id`, `name`, `description`, `variables` (same list shape as `params`).
  Only the variables of a top-level folder are used (collection variables); nested folders keep the key for later.
- **Globals** live in `<workspace>/globals.yaml`: `version: 1`, `variables`. A missing file means no globals.
- **Variable precedence**, highest first: active environment, collection variables, globals, dynamic variables.
- **Query string**: the stored request `url` never contains the query of the params table; the editor shows the URL
  with the enabled params appended and keeps both in sync (M2-13). A legacy URL that already contains `?a=b` is read
  as is; the editor moves that inline query to the front of the params list on load.
- **History** is per-user and never committed: `.yaapic/history.jsonl`, one JSON object per line, written with
  `System.Text.Json` source generators (JSON is acceptable here: the file is append-only, machine-owned and not
  hand-edited, so ADR 0005's reasons for YAML do not apply). Fields: `id` (GUID "D"), `at` (UTC, ISO 8601 round trip),
  `request` (the request as a YAML string, written by `RequestYamlSerializer`), `statusCode` (int or null),
  `statusText`, `durationMs`, `sizeBytes`, `failureKind` (enum name or null), `failureMessage` (string or null).
  Response bodies are not stored. Retention: the last 500 entries; the file is rewritten when it exceeds 550 lines.
- **Secrets**: auth values and variables are stored in clear in the workspace (the secret vault arrives in M3); the
  UI hints at using `{{variables}}`. History keeps the request exactly as sent, so it may hold credentials; it lives
  under `.yaapic/`, which is git-ignored.
- Deferred, not designed here: manual ordering and drag and drop, cookie jar, proxy and TLS settings, workspace picker,
  autocomplete of `{{variables}}`, request-level scripts (M3).

## Out of scope

Any code. Later cards implement and test each rule.

## Acceptance criteria

- [x] ADR 0008 exists with the sections above; `docs/adr/README.md` lists it as Accepted.
- [x] `docs/file-format.md` shows the full v1 request, folder and globals files, the history line, the ordering rule,
      the query string rule and the precedence rule, with the same level of detail as today.
- [x] No code change; `dotnet build Yaapic.sln -c Release` still has 0 warnings.

## Suggested commit message

`docs: add ADR 0008 and milestone 2 file format extensions`
