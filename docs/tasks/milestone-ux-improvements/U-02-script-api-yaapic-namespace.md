# U-02: `yaapic` script namespace replaces `pm`

Status: done
Platform: any
Depends on: none

## Goal

Give the script API its own identity: scripts use `yaapic.*` (`yaapic.variables`, `yaapic.request`, `yaapic.response`, `yaapic.test`,
`yaapic.expect`, `yaapic.sendRequest`, `yaapic.cookies`, `yaapic.info`). Postman's `pm` is no longer defined; Postman and Bruno scripts
are converted when they are brought in (U-05).

## Design decisions

- One name, `yaapic`, defined once in each engine's prelude (JavaScript: `QuickJsPrelude` and the `Pm*Prelude` files; Lua: the
  `LuaPm*Prelude` files). The API surface and behaviour are unchanged from the former `pm`.
- The Postman conformance suites (M3-23, M3-32) now run against `yaapic`; the cases still mirror the Postman documentation.
- Docs, default script templates, palette hints, completion and screenshots use `yaapic`; the compatibility page says that a Postman script
  replaces `pm` with `yaapic`.
- The prelude file names keep their `Pm` prefix; the host protocol and the C# types (`ScriptHost`, `HostBridge`) do not change.

## Out of scope

Importing or converting scripts (U-05), the legacy `postman.*` aliases (left as they are).

## Acceptance criteria

- [x] Every member covered by the conformance suites works as `yaapic.x` in JavaScript and Lua.
- [x] `pm` is not defined in either engine.
- [x] Docs, README, templates, hints and completion show `yaapic` only.
- [x] Full build and test run green.

## Suggested commit message

`feat: rename the script namespace from pm to yaapic`
