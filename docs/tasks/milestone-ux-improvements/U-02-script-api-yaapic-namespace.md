# U-02: `yaapic` script namespace with `pm` as an alias

Status: done
Platform: any
Depends on: none

## Goal

Give the script API its own identity: scripts use `yaapic.*` (`yaapic.variables`, `yaapic.request`, `yaapic.response`, `yaapic.test`,
`yaapic.expect`, `yaapic.sendRequest`, `yaapic.cookies`, `yaapic.info`). Postman's `pm` stays as an alias of the same object, so imported
Postman scripts keep working unchanged.

## Design decisions

- One object, two names: `pm` is the same reference as `yaapic`, defined once in each engine's prelude (JavaScript: `QuickJsPrelude` and
  the `Pm*Prelude` files; Lua: the `LuaPm*Prelude` files). No behaviour differs between the two names.
- The Postman conformance suites (M3-23, M3-32) keep running against `pm`; add a parallel set of assertions that `yaapic` resolves to the
  identical members.
- Docs, default script templates, palette hints and screenshots use `yaapic`; `pm` is documented as the compatibility name.
- Rename the prelude files only if it reads better; the host protocol and the C# types (`ScriptHost`, `HostBridge`) do not change.

## Out of scope

Removing `pm`, warning when `pm` is used, importing scripts (M5 rewrites nothing).

## Acceptance criteria

- [x] `yaapic.x` and `pm.x` give the same result in JavaScript and Lua for every member covered by the conformance suites.
- [x] `yaapic === pm` (JavaScript) and `yaapic == pm` (Lua).
- [x] Docs and the scripting section of the README show `yaapic` first and mention `pm`.
- [x] Existing scripts using `pm` still pass (full test run).

## Suggested commit message

`feat: add the yaapic script namespace with pm as an alias`
