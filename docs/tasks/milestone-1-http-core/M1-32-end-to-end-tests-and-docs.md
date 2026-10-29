# M1-32: End-to-end tests and milestone docs

Status: todo
Platform: any
Depends on: M1-23

## Goal

Prove the HTTP core end to end over a real loopback socket, and document what M1 delivers.

## Design decisions

- Tests in `tests/Yaapic.Infrastructure.Tests/EndToEnd/`, no external network: a loopback server built on
  `System.Net.HttpListener` bound to `http://127.0.0.1:<free port>/` (find a free port with a `TcpListener` on port 0,
  then release it), started per test class, echoing method, path+query, headers and body as JSON, plus routes
  `/status/{code}` and `/delay/{ms}`.
- Scenarios, through `AppComposition.CreateShell` on a temp workspace with `FakeDialogService` (only the shell and
  its ViewModels drive the flow, like the UI would):
  1. GET with a query param and a header: echo shows both; response `200`, pretty JSON indented.
  2. POST raw JSON body: echo shows the body and `Content-Type: application/json`.
  3. Environment variable `baseUrl` = server URL, request `{{baseUrl}}/echo`: resolved; with no active environment the
     report lists `baseUrl` as unresolved and the result is a failure (no response).
  4. `/status/500` -> `ServerError` category; `/delay/2000` canceled after 100 ms -> "Request canceled".
  5. Save a request, then build a **new** shell on the same workspace and `InitializeAsync`: the request is listed and
     its tab is restored and active.
- Docs: `docs/architecture.md` (new, short): projects and their allowed references (the `DependencyRulesTests` table),
  the request flow (ViewModel -> `RequestSender` -> `RequestResolver` -> `IHttpTransport`), storage adapters and
  `docs/file-format.md` link. In `docs/tasks/README.md`, nothing else changes beyond this card's status.

## Out of scope

UI automation, performance tests, external hosts.

## Acceptance criteria

- [ ] The five scenarios pass on Windows and Linux runners (no Windows-only API: `HttpListener` works on both).
- [ ] `docs/architecture.md` exists and matches the code.
- [ ] 0 warnings, all tests green.

## Suggested commit message

`test: add HTTP core end-to-end tests and architecture doc`
