# ADR 0008: Collections, globals and history storage

Status: Accepted

## Context

Milestone 2 adds collections (folders of requests), global variables, collection variables, a full request model
(methods, body kinds, auth, settings) and a send history. Storage must stay readable, diffable and hand-editable like
ADR 0005, keep milestone 1 workspaces working without migration, and remain Native AOT friendly.

## Options

1. **Directories for collections, one YAML file per node, history as JSON lines.** Git sees one file per request;
   folders map to the file system; history is machine-owned and kept out of Git.
2. **One YAML file per collection** holding all its requests. Fewer files, but large diffs, merge conflicts and a
   hand-edited monolith.
3. **A local database (SQLite) for everything.** Fast queries, but binary, not diffable and a native dependency.

## AOT risks

Folder and globals files reuse the VYaml `[YamlObject]` approach of ADR 0005 (same caveats, same AOT publish check).
History uses `System.Text.Json` with source generators only (`JsonSerializerContext`), which is Native AOT safe; no
reflection-based serialisation. The `request` field holds the request as a YAML string produced by
`RequestYamlSerializer`, so no second request serializer exists.

## Decision

Option 1.

- **Collections are directories.** `requests/` stays the root. A directory under `requests/` containing a
  `folder.yaml` is a folder; a top-level folder is a collection (only the UI differs). Requests directly in
  `requests/` are unfiled and unchanged. A directory without `folder.yaml` is ignored and reported as a listing error.
- **Names on disk** use the ADR 0005 slug rule (`-2`, `-3` on collision among siblings); renaming a folder or request
  renames its directory or file.
- **Order**: folders first, then requests, each by name (ordinal, ignoring case), then by id. No manual ordering or
  drag and drop in M2.
- **Format version stays 1.** New keys are optional on read and always written; unknown keys are ignored.
- **Folder file** `folder.yaml`: `version`, `id`, `name`, `description`, `variables`. Only top-level folder variables
  are used (collection variables).
- **Globals**: `<workspace>/globals.yaml` (`version`, `variables`); a missing file means no globals.
- **Variable precedence**, highest first: active environment, collection variables, globals, dynamic variables.
- **Query string**: the stored `url` never contains the params-table query; the editor appends enabled params and keeps
  both in sync (M2-13). A legacy inline query is read as is and moved to the front of the params list on load.
- **History**: `.yaapic/history.jsonl`, per-user, git-ignored, one JSON object per line. JSON is acceptable here
  because the file is append-only, machine-owned and not hand-edited, which removes ADR 0005's reasons for YAML.
  Response bodies are not stored. Retention: last 500 entries; the file is rewritten when it exceeds 550 lines.
- **Secrets**: auth values and variables are stored in clear (the vault arrives in M3); the UI hints at `{{variables}}`.
  History keeps the request as sent and may hold credentials, hence `.yaapic/` being git-ignored.
- **Deferred**: manual ordering and drag and drop, cookie jar, proxy and TLS settings, workspace picker, `{{variable}}`
  autocomplete, request scripts (M3).

Format details: [../file-format.md](../file-format.md)
