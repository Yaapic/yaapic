# M2-66: End-to-end tests and milestone docs

Status: done
Platform: any
Depends on: M2-07, M2-31, M2-45, M2-57, M2-63

## Goal

Prove milestone 2 end to end over the loopback server and bring the docs in line with the code.

## Design decisions

- Extend `tests/Yaapic.Infrastructure.Tests/EndToEnd/` (same `LoopbackServer`, temp workspace, `FakeDialogService`,
  `AppComposition.CreateShell`, no external network). Add a `/redirect` route (302 to `/echo`) to the server if missing.
  Scenarios:
  1. PUT with a url-encoded body and PATCH with a raw JSON body: the echo shows the verb, body and content type.
  2. Multipart form-data with a text and a file field: the echo shows both parts; a binary file body arrives byte for byte.
  3. Basic auth and API key (query): the echo shows `Authorization: Basic ...` and the query parameter.
  4. Redirects: followed by default, returned as 302 with `Location` when `FollowRedirects` is false.
  5. Collections: create a collection and a nested folder through `Shell.Collections` commands (prompt answers scripted),
     create a request in it, move it to the top level, rename and delete it; the directories and files on disk match at each
     step.
  6. Scopes: the same `{{name}}` defined in globals, in the collection and in the active environment resolves to the
     environment value, then to the collection value once the environment is cleared, then to the globals value once the
     request is moved out of the collection.
  7. History: three sends are recorded newest first, an entry reopens as a new tab, clear empties the file; a restart
     (new shell, same workspace) still shows the history.
  8. Palette: opening it and typing part of a request name lists that request first; executing it opens the tab.
- Docs: update `docs/architecture.md` (new ports `ICollectionStore`, `IGlobalsStore`, `IHistoryStore`, `Core.Search`,
  the palette, variable scopes in the request flow and the new storage adapters), check `docs/file-format.md` against the
  files the tests produced (fix any difference), add the M2 summary and the deferred list of ADR 0008 to
  `docs/tasks/README.md` under the M2 table (replace the placeholder sentence), and state in `docs/dependencies.md` that M2
  added no package.

## Out of scope

UI automation, performance tests, external hosts.

## Acceptance criteria

- [x] The eight scenarios pass on Windows and Linux runners.
- [x] `docs/architecture.md`, `docs/file-format.md` and `docs/dependencies.md` match the code.
- [x] 0 warnings, all tests green.

## Suggested commit message

`test: add milestone 2 end-to-end tests and docs`
