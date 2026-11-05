# M3-64: End-to-end tests: JavaScript scripting

Status: todo
Platform: any
Depends on: M3-47, M3-44, M3-23

## Goal

Prove the JavaScript scripting stack end to end over the loopback server, through the shell view model, with the real
QuickJS engine, real files and the real stores.

## Design decisions

- Extend `tests/Yaapic.Infrastructure.Tests/EndToEnd/` (same `LoopbackServer`, `TempWorkspace`, `FakeDialogService`,
  `AppComposition.CreateShell`, no external network). New file `Milestone3ScriptingEndToEndTests.cs` (a shared base or helper
  `ScriptedShellFixture` for building a workspace with requests, folders and environments written as YAML on disk, so the file
  format is exercised too). Add to the loopback server if missing: `/json` (a small JSON document with a `token` field and an
  array), `/headers` (echo request headers as JSON), `/set-cookie` (two `Set-Cookie` headers), `/count` (a counter incremented
  per call, returned as JSON), `/log-heavy` is **not** needed.
- Tests need the native libraries: they run in CI after `Build-Natives.ps1`; locally they run when `artifacts/native/<rid>` exists
  and otherwise **fail with a message that names the script** (do not skip silently: a missing engine in CI must be red).
- Scenarios (each a `[Fact]`):
  1. **Pre-request mutation**: a request with a pre-request that adds a header, changes a query param and sets a variable used in
     the URL; the echo shows all three.
  2. **Token chaining**: request A's post-response saves `pm.response.json().token` with `pm.environment.set`; request B uses
     `{{token}}` in an `Authorization` header; the value is in the environment file on disk after A and arrives in B's echo;
     B's URL highlighting context resolves the variable (`Variables.Tokenize`).
  3. **Order**: collection, folder and request scripts append to a collection variable-free log through `console.log`; the console
     shows them in collection, folder, request order for pre-request, and the same order for post-response.
  4. **Tests**: five `pm.test` calls (3 pass, 2 fail with Chai messages) fill `Response.Tests` with counts `3/5`, summary text, and
     the failing messages; the response body is intact.
  5. **Errors**: a pre-request throwing an error aborts the send (the loopback server records **no** hit) with the error state and the
     script error text including line; a post-response throwing keeps the response and lists the error; an infinite loop in a pre-request
     is `TimedOut` within the limit (use a shortened `ScriptLimits` injected through the composition test seam) and no leaked thread
     afterwards (the next send works).
  6. **pm.sendRequest**: a pre-request fetches `/json` and uses its `token` in a header of the main request; the console shows the
     network line of the sub-request; callback and `await` forms both work; the 21st call fails.
  7. **Variable scopes**: `pm.globals.set`, `pm.collectionVariables.set` and `pm.environment.set` land in `globals.yaml`,
     `folder.yaml` and the environment file respectively; `pm.variables.set` is local (gone on the next send); a missing environment
     falls back to local with the console warning.
  8. **Cookies**: `/set-cookie` response, `pm.cookies.get` sees both cookies in post-response and none in pre-request;
     `pm.cookies.jar()` error text.
  9. **Persistence round trip**: scripts typed into the editor view model are saved to the request YAML, a **restart** (new
     shell, same workspace) loads them and they still run.
  10. **Cancel**: cancelling a slow script (a loop awaiting nothing, 5 s limit) returns promptly with "Request canceled".

## Out of scope

Lua (M3-65), vault (M3-66), UI automation, performance tests.

## Acceptance criteria

- [ ] The ten scenarios pass on Windows and Linux runners (macOS too when its CI job runs the suite).
- [ ] Existing end-to-end tests untouched and green; 0 warnings.
- [ ] No leaked `yaapic-js` threads after the suite (assert thread count with a small tolerance in one test).

## Rules

- Every shell command, git included, starts with `rtk` (`rtk bypass <cmd>` only if the condensed output is unusable).
- Wait for long commands (build, test, AOT publish) in the foreground; do not poll.
- Stop every process you launched (published exes, stub servers, `dotnet` build servers you started).
- Check the SonarCloud issues of the files you changed
  (`https://sonarcloud.io/api/issues/search?componentKeys=Yaapic_yaapic&resolved=false`) and fix them before committing.
- Do not skip any acceptance item; if one cannot be met, stop and say why instead of ticking it.
- Commit on `feat/m3-scripting` (no new branch or worktree), GPG-signed
  (`git -c gpg.program="C:\Program Files (x86)\GnuPG\bin\gpg.exe" commit -S`, key of adrien.granger@outlook.com),
  with no trailer or co-author line, then push.

## Suggested commit message

`test: add JavaScript scripting end-to-end tests`
