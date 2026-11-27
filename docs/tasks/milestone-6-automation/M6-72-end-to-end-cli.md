# M6-72: End-to-end tests: command-line runner

Status: todo
Platform: any
Depends on: M6-71

## Goal

Run realistic workspaces through `CliProgram.RunAsync` with the real engines, stores and loopback servers, as M6-57 does for the window:
scripts, data, flow control, authentication, secrets and the read-only rule.

## Design decisions

- Read first: M6-57 and `tests/Yaapic.Infrastructure.Tests/EndToEnd/Milestone6RunnerEndToEndTests.cs` (same scenarios, app side),
  `LoopbackServer` (echo, `/status/{code}`, `/delay/{ms}`), `AuthStubServer`, `ScriptedShellFixture` (engine skip rules), ADR 0022, M6-70,
  M6-71.
- `tests/Yaapic.Cli.Tests/EndToEnd/CliEndToEndTests.cs`. Workspaces are written with the Infrastructure file stores into a temp directory;
  link `AuthStubServer.cs` like the other shared helpers (M6-70). Every test ends by checking that the workspace tree is unchanged and that
  no `.yaapic/` directory exists.
- Scenarios (assert exit code, stdout/stderr lines and the JSON report):
  1. collection pre-request script (JavaScript) + folder pre-request script (Lua) + request post-response test: both chain levels run, in
     order (collection first), seen through echoed headers;
  2. a CSV file (3 rows, a quoted comma) drives `{{user}}` in the URL and `yaapic.iterationData.get('user')` in a JavaScript test and
     `yaapic.iteration_data.get('user')` in a Lua test; 3 iterations, exit 0;
  3. a post-response script sets `token` in the environment, the next request sends it (echoed); the environment file on disk is unchanged;
  4. `setNextRequest` jumps forward (a request is not run); an unknown name: exit 1, `run error: setNextRequest: no request named '...'` on
     stderr, status `failed`;
  5. `skipRequest` in a folder pre-request script: `SKIP` line, the server never receives that request;
  6. `--stop-on-failure` after a failed test: the later requests do not run, status `stoppedOnFailure`, exit 1;
  7. Basic auth whose password is `{{password}}`, a secret environment row with no value: the warning line on stderr; with
     `YAAPIC_VAR_password` set, the echoed `Authorization` header decodes to the value, and the plain value appears in neither stdout nor
     the JSON and JUnit reports (only the plain value is masked: its base64 form is the accepted ADR 0010 limit, do not assert on it);
  8. OAuth 2.0 client credentials against `AuthStubServer` with the client secret from `YAAPIC_VAR_clientSecret`: passes; an
     authorization-code request: `ERROR` line with the ADR 0022 browser message, exit 1;
  9. `--delay 300` between two requests: the second starts at least 300 ms after the first ended (timestamps from the server).
- Tests that need the native engines follow the skip rules of the milestone 3 end-to-end tests.

## Out of scope

The published exe and packaging (M6-73), docs (M6-74).

## Acceptance criteria

- [ ] The test class with the scenarios above passes locally and in the CI configuration (no external network; `Yaapic.Cli.Tests` is in
  `Yaapic.sln` and `Yaapic.CrossPlatform.slnf`, so CI runs it on the three OSes).
- [ ] Architecture tests green, 0 warnings, all tests green.
- [ ] Docs: `docs/architecture.md`, section "End-to-end tests": add one line for `CliEndToEndTests` (what it covers).

## Suggested commit message

`test: add end-to-end tests for yaapic-cli`
