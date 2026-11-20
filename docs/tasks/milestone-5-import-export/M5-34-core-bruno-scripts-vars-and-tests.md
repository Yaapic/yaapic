# M5-34: Core: Bruno import, scripts, variables and tests

Status: done
Platform: any
Depends on: M5-33

## Goal

Bruno scripts, `vars:*` blocks and `tests` blocks become Yaapic scripts through `ScriptConverter`.

## Design decisions

- Read first: M5-33, U-05 and `src/Yaapic.Core/Scripting/ScriptConverter.cs` (`ScriptDialect.Bruno`; `bru.setVar` maps to `yaapic.variables.set`), `docs/scripting-compatibility.md` (Bruno table), M5-15 (the same finding report format).
- `src/Yaapic.Core/Import/Bruno/BrunoScripts.cs` (internal static): `ScriptSet Map(BruFile file, string path, ImportReport report)`, applied to requests, folders (`folder.bru`) and the collection (`collection.bru`).
- Build the **Bruno-dialect source** of each phase, then convert it once with `ScriptConverter.Convert(source, ScriptDialect.Bruno, ScriptLanguage.JavaScript)`:
  - **Pre-request** = the lines generated from `vars:pre-request` (one per enabled pair, in order: `bru.setVar(<json-quoted key>, <json-quoted value>);`, the value written as a JSON string literal built with `DataNodeJson.Write` of a String node), then the text of `script:pre-request`, separated by a blank line.
  - **Post-response** = the lines from `vars:post-response` (the value is a Bruno **expression**, e.g. `res.body.token`: `bru.setVar(<json-quoted key>, <value as written>);`), then the `script:post-response` text, then the `tests` text, each part separated by a blank line.
  - Disabled pairs (`~`) are skipped. Empty parts are skipped; all parts empty gives `ScriptBlock.Empty`.
- Findings -> `Warning` `Strings.ImportScriptFinding(path, phaseName, member, line, reason)` (string from M5-15). The aggregated `Strings.ImportScriptsReview(count)` `Warning` is added once per import when any non-empty script exists (shared string, M5-15; count = number of non-empty blocks).
  The line numbers refer to the generated source (the generated `vars` lines come first); say so in the XML doc.
- **Collection and folder level**: `collection.bru` and `folder.bru` scripts go to the collection folder's and the folder's `Scripts`, as in the Yaapic script chain (collection, folders outermost to innermost, request).
- **Collection variables**: Bruno collection and folder variables are the `vars:pre-request` pairs of `collection.bru` / `folder.bru`; they are **runtime variables set by script**, so they become the generated `bru.setVar` lines above (not `Variables` rows): the collection folder's and the folders' `Variables` stay empty for Bruno.
  State this in the XML doc and in the docs card (M5-67).
- `BrunoRequests` and `BrunoCollection` call `BrunoScripts.Map` and set `Scripts`.

## Out of scope

Converting `assert` blocks to tests (dropped, M5-32), Bruno `docs`, CommonJS `require` support.

## Acceptance criteria

- [x] Tests: pre-request vars + script combined with exact converted text (`bru.setVar` -> `yaapic.variables.set`); post-response vars with `res.body.id` -> the converted expression; tests block appended after the post-response script; a disabled var skipped; an unsupported member (`bru.runRequest`) warned with member and line;
  empty file gives `ScriptSet.Empty`; the review warning once; collection and folder scripts land on the folders; request scripts on requests.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): import Bruno scripts, vars and tests`
