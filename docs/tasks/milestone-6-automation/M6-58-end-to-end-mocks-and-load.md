# M6-58: End-to-end tests: mock servers and load tests

Status: todo
Platform: any
Depends on: M6-42

## Goal

Drive mock servers and load tests through the real shell, with real sockets on the loopback interface.

## Design decisions

- Read first: M6-57 (fixture), ADRs 0019 and 0020, M6-42, `tests/Yaapic.Infrastructure.Tests/Mocks/TcpMockHttpHostTests.cs` (free-port helper).
- `tests/Yaapic.Infrastructure.Tests/EndToEnd/Milestone6MocksAndLoadEndToEndTests.cs`:
  - **Mocks**: create a mock server through `shell.Mocks` on a free port with routes `GET /users/:id` (templated body and header),
    `POST /echo` returning `{{body}}`, `ANY /files/*`, a disabled route, a delayed route (200 ms); start; send requests **from Yaapic requests**
    (the request editor, so the real transport is used) and assert status, headers, bodies, the CORS header, the preflight answer, the 404 body,
    `HEAD` without a body, the delay (at least 200 ms), keep-alive (two sends, one connection if observable through the log timing; otherwise skip
    that assertion); the log lists the requests with route names; editing and saving a route changes the next answer while running; the
    `mocks/*.mock.yaml` file on disk matches the definition; generating from a folder creates the routes; stopping frees the port; closing the shell
    stops every server.
  - **Runner against a mock**: a folder whose requests target the mock (`{{baseUrl}}` = the mock URL) runs green in the Runner.
  - **Load test**: against the mock server, Fixed 5 users for 10 s with think time 100 ms (confirmation answered yes through
    `FakeDialogService`), then RampUp 10 users over 5 s for 10 s: counts > 0, error rate 0, per-second buckets, active users rising during the
    ramp, the report JSON parses; a route returning 500 gives a 100 % error rate; Stop cancels early; declining the confirmation starts nothing.
    Keep the whole class under 60 s of wall time.

## Out of scope

UI tests, monitors (M6-59).

## Acceptance criteria

- [ ] The test class passes locally and in the CI configuration (loopback only, no external network); no port left open afterwards.
- [ ] Architecture tests green, 0 warnings, all tests green.

## Suggested commit message

`test: add end-to-end tests for mock servers and load tests`
