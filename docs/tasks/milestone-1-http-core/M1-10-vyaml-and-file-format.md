# M1-10: VYaml adoption and workspace file format v1

Status: todo
Platform: any
Depends on: M1-08

## Goal

Add VYaml to `Yaapic.Infrastructure`, prove it under Native AOT (as ADR 0005 requires), and document the workspace
file format v1 that M1-12 to M1-15 implement.

## Design decisions

- Package: `VYaml`, latest stable version on nuget.org at implementation time, pinned in `Directory.Packages.props`,
  referenced by `Yaapic.Infrastructure` only, and justified in `docs/dependencies.md` (MIT, source-generated, AOT).
- AOT proof: in the scratchpad (never committed), a console project with `PublishAot=true` that references
  `Yaapic.Infrastructure`, round-trips one `[YamlObject]` type containing a string, a nested `[YamlObject]`, a
  `List<T>` of objects and a multi-line string, published with `-r <host rid>` and run. Record in `docs/dependencies.md`:
  VYaml version, publish result (number of IL2xxx/IL3xxx warnings, must be 0) and whether multi-line strings are emitted
  as literal block scalars (`|`) or as quoted strings with `\n`. If AOT fails, **stop** and report: ADR 0005's fallback
  (JSON) is a product decision.
- Committed code in this card: only the package reference. The DTOs come with M1-12.
- Write `docs/file-format.md` (English) with exactly this content model:

  ```
  <workspace>/
    .gitignore                      contains ".yaapic/"
    .yaapic/session.yaml            per-user state, never committed
    requests/<slug>.request.yaml    one request per file
    environments/<slug>.env.yaml    one environment per file
  ```

  Request file:

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

  Environment file: `version: 1`, `id`, `name`, `variables` (same list shape as `params`).
  Session file: `version: 1`, `openRequests` (list of GUID strings), `activeRequest` (GUID or empty),
  `activeEnvironment` (GUID or empty).
  Rules to document: UTF-8 without BOM, `\n` line endings, keys in the order shown, every key always written (empty
  lists as `[]`), unknown keys ignored on read, missing optional keys take defaults (`enabled: true`, `body.type: none`),
  a missing or different `version` is an error for request and environment files. Slug rule: lowercase the name, replace
  every run of characters other than `a-z` and `0-9` with `-`, trim `-`, cut to 60 characters, empty -> `request` or
  `environment`; on collision with another id append `-2`, `-3`, ...
- Link `docs/file-format.md` from ADR 0005 ("Format details: ../file-format.md").

## Out of scope

Collections and folders (M2), secrets, migrations between versions, the DTOs and stores themselves.

## Acceptance criteria

- [ ] `docs/dependencies.md` has the VYaml row and the AOT check result (0 IL2xxx/IL3xxx warnings, binary ran).
- [ ] `docs/file-format.md` exists as specified and ADR 0005 links it.
- [ ] Build 0 warnings; no scratch project committed.

## Suggested commit message

`docs: adopt VYaml and document workspace file format v1`
