# M5-40: Core: Postman export, scripts

Status: done
Platform: any
Depends on: M5-39

## Goal

Yaapic scripts are written as Postman `event`s, with `yaapic.` rewritten to `pm.`.

## Design decisions

- Read first: M5-15 (the inverse), `src/Yaapic.Core/Scripting/ScriptConverter.cs` (its scanner skips strings, comments and template literals; this card needs the same behaviour in the other direction), `docs/scripting-compatibility.md`, `src/Yaapic.Domain/Scripting/ScriptSet.cs`.
- `src/Yaapic.Core/Scripting/YaapicToPostmanRewriter.cs`, `public static class YaapicToPostmanRewriter`, `string Rewrite(string source)`: a small scanner (own code, ~60 lines) that copies the source and replaces the identifier `yaapic` by `pm` when it is a **whole identifier** (the previous character is not a letter, digit, `_`, `$` or `.`) and is immediately followed by `.`.
  It skips, copying unchanged: line comments `// ...`, block comments `/* ... */`, single- and double-quoted strings (honouring `\` escapes) and template literals `` `...` `` (honouring `\` escapes; `${ ... }` expressions inside are **not** rewritten: documented limit). Regular-expression literals are not recognised (documented limit).
  Nothing else changes (members that exist only in Yaapic stay and will fail in Postman).
- `src/Yaapic.Core/Export/Postman/PostmanScriptWriter.cs` (internal static): `DataNode? Events(ScriptSet scripts, string path, ImportReport report)`: array of `{ "listen": "prerequest"|"test", "script": { "type": "text/javascript", "exec": [ lines ] } }` in that order (`prerequest` first), only for non-empty blocks; the source is rewritten for JavaScript blocks and
  split on `\n` into the `exec` array (a trailing newline does not add an empty line; `\r\n` normalized). A **Lua** block is not exported: `Warning` `Strings.ExportPostmanLuaScript(path)` ("{0}: a Lua script cannot be exported to Postman and was left out."). Returns null when nothing remains.
- Written at all three levels by the exporter: the collection folder's scripts -> top-level `event`; each folder -> its `event` (after `item`); each request -> `event` (after `response`). Member order for items: `name`, `item` or `request`, `response`, `event`; top-level: `info`, `item`, `auth`? (never), `event`, `variable`.
- One `Info` per export when any script was written: `Strings.ExportScriptsRewritten` ("Scripts were exported with the yaapic API renamed to pm; check them in Postman.").
- Strings (EN and FR): `export.postman.luaScript`; `export.scriptsRewritten`.

## Out of scope

Reverse conversion of other APIs, Lua to JavaScript, `console` differences.

## Acceptance criteria

- [x] Tests for the rewriter: `yaapic.environment.set("a", 1)` -> `pm.environment.set(...)`; the word inside a string, a comment, a template literal text unchanged; `myyaapic.x` and `a.yaapic.b` unchanged; `yaapic` without a dot unchanged; two statements; `yaapic.test("x", () => {})`; empty text; text without `yaapic`.
- [x] Tests for events: both phases, only one, empty, Lua skipped with the Warning, multi-line source split, CRLF; the three levels in a full export compared with exact JSON; the aggregated `Info`.
- [x] Round trip with `PostmanScripts` (M5-15): export then import gives the original JavaScript for a script that uses only members both sides know; if M5-15 is not `done`, leave it to M5-66.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): export scripts to Postman events`
