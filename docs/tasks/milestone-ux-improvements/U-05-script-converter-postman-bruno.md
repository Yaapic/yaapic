# U-05: Convert Postman and Bruno scripts to `yaapic`

Status: todo
Platform: any
Depends on: U-02

## Goal

A "Paste a Postman or Bruno script" action in the script editor turns pasted code into `yaapic` code, and lists what could not be
converted. The same converter is reused by the M5 importers (Postman collections, Bruno collections).

## Design decisions

- A pure function in `Yaapic.Core` (no UI): `ScriptConverter.Convert(source, dialect, language)` returns the converted text and a list of
  findings (line, member, reason).
- Postman: `pm.` becomes `yaapic.`; the legacy `postman.*` calls are mapped to their `yaapic` equivalents; members Yaapic does not
  support (`pm.execution`, `jsonSchema`, `pm.vault`, ...) are left in place and reported.
- Bruno: an explicit table maps `bru.getEnvVar`, `bru.setEnvVar`, `bru.getVar`, `bru.setVar`, `req.setHeader`, `req.setUrl`,
  `req.setBody`, `res.getBody()`, `res.status`, `test()`/`expect` and the other documented members; unknown members are reported.
- Token-level rewriting with a small tokenizer (strings, comments and template literals are skipped); no reflection, no external
  JavaScript parser (Native AOT).
- UI: a paste dialog in the script editor, dialect auto-detected (`pm.`/`postman.` versus `bru.`/`req.`/`res.`) and overridable, with a
  preview of the result and the findings.

## Out of scope

Importing whole collections (M5), Lua conversion of Bruno scripts (Bruno scripts are JavaScript only).

## Acceptance criteria

- [ ] Tests for the converter: every Postman member covered by the conformance suites, the mapped Bruno members, strings and comments
  left alone, unsupported members reported.
- [ ] The paste dialog converts, shows the findings and inserts the result in the editor.
- [ ] Docs: `docs/scripting-compatibility.md` describes the conversion and its limits.

## Suggested commit message

`feat: convert Postman and Bruno scripts to the yaapic API`
