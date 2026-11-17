# M5-15: Core: Postman import, scripts and collection variables

Status: todo
Platform: any
Depends on: M5-12

## Goal

Postman `event` scripts become Yaapic scripts through `ScriptConverter`, and collection and folder `variable` arrays become folder variables.

## Design decisions

- Read first: M5-12, U-05 and `src/Yaapic.Core/Scripting/ScriptConverter.cs` (`Convert(source, ScriptDialect.Postman, ScriptLanguage.JavaScript)` returns `ScriptConversionResult(Text, Findings)`), `docs/scripting-compatibility.md`, ADR 0009 (script chain: collection, folders, request).
- `src/Yaapic.Core/Import/Postman/PostmanScripts.cs` (internal static): `ScriptSet Map(DataNode? events, string path, ImportReport report)`; `ScriptSet.Empty` for none.
  - Each element of `event`: skip when `disabled` is true; `listen` `prerequest` -> `PreRequest`, `test` -> `PostResponse`, other values ignored. `script.exec` is an array of lines (joined with `\n`) or a string; `script.type` other than empty or `text/javascript` -> `Skipped` entry and ignored.
  - Several events with the same `listen` on one item: joined by a blank line in source order.
  - Convert the text with `ScriptConverter.Convert(text, ScriptDialect.Postman, ScriptLanguage.JavaScript)`; `Text` becomes the block source (`ScriptBlock(ScriptLanguage.JavaScript, text)`); every finding is
    `Warning` `Strings.ImportScriptFinding(path, phaseName, finding.Member, finding.Line, finding.Reason)` where `phaseName` is `pre-request` or `post-response`.
  - Empty or whitespace-only sources produce an empty block.
- Applied at three levels by `PostmanItems`: collection `event` -> collection folder `Scripts`; folder `event` -> that folder's `Scripts`; request `event` -> `HttpRequestDefinition.Scripts`.
- **Aggregated script warning**: when at least one non-empty script was imported, add **one** `Warning` per import `Strings.ImportScriptsReview(count)` ("{0} scripts were imported. Review them before sending: scripts run with access to your variables.").
- **Variables**: `variable` array of `{key, value, disabled, type}` on the collection and on each folder -> `KeyValueEntry(key, TranslateDynamicVariables(value as string ("" when null)), Enabled = !disabled)`; elements without a key dropped.
  `type` `secret` is honoured: `IsSecret = true` (only on the collection folder; for nested folders the flag is not set, the value is kept; the applier blanks secret rows of nested folders anyway). Variable values that are not strings (numbers, booleans) use `AsString`.
- Strings (EN and FR): `import.scriptFinding` = "{0}: {1} script, {2} (line {3}): {4}"; `import.scriptsReview` shared by every format (`import.scriptsReview`).

## Out of scope

Bodies and auth; converting Postman `pm.sendRequest` flows beyond what `ScriptConverter` does; Postman `test` snippets for the UI.

## Acceptance criteria

- [ ] Tests: pre-request and test events on the collection, a folder and a request land in the right `Scripts`; `exec` array and string forms; two prerequest events joined; disabled event skipped; non-JavaScript `script.type` skipped with the entry; a `pm.environment.set(...)` converted to `yaapic.environment.set(...)`;
  an unsupported member (`pm.execution.skipRequest`) produces a `Warning` with member and line; the aggregated review warning appears once; empty scripts add nothing; collection variables incl. disabled, null value and a numeric value; a secret collection variable gets `IsSecret`; nested folder variables are kept.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): import Postman scripts and variables`
