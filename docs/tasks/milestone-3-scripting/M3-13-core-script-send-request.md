# M3-13: Core: host support for `pm.sendRequest`

Status: done
Platform: any
Depends on: M3-12

## Goal

Implement `IScriptHost.Send`: a script can issue extra HTTP requests through the application transport, with limits, the
current variable scopes and a console trace.

## Design decisions

- Read first: M3-11/M3-12 (`ScriptHost`), `RequestSender`, M3-09 (`ScriptRequest.ApplyTo`).
- `ScriptedRequestSender` passes `ScriptHost` a `ScriptSendService` (internal class in `Yaapic.Core.Scripting`) built per
  `SendAsync`: counts sends (`ScriptLimits.MaxSendRequests`, then `Error = "pm.sendRequest limit of 20 per run reached"`),
  converts the `ScriptRequest` to a new `HttpRequestDefinition.Create("pm.sendRequest")` with `ApplyTo`, resolves
  `{{variables}}` through the **current overlay scopes** (`overlay.ToScopes()`), calls
  `RequestSender.SendAsync(definition, scopes, ct)` with the send's cancellation token, and returns
  `ScriptSendResult(ScriptResponse.FromData(response), null)` or `(null, failure message)`. Blocking: the host call
  waits with `.GetAwaiter().GetResult()` (it runs on the engine thread, never the UI thread; XML-doc that and the
  deadlock-free reason: `ConfigureAwait(false)` is already used by `RequestSender`).
- A sent sub-request does **not** run scripts (no recursion) and is **not** recorded in the history; it appends one
  `ConsoleEntry(Network)` to `NetworkLog` marked with the origin `"pm.sendRequest"`.
- Per-call timeout: the sub-request uses the transport default (30 s); the surrounding block timeout (5 s default) still
  applies, so the engine's interrupt must also fire while the host blocks: `IScriptHost.Send` must observe the invocation's
  cancellation. Add an optional `CancellationToken` to `ScriptSendService` that the engine adapter cancels on timeout
  (the engine passes its own linked token through `ScriptInvocation`; add `CancellationToken` as a property of
  `ScriptHost`, set by `ScriptedRequestSender` per run, and document that engines cancel it on timeout).
- The `SendReport.UnresolvedVariables` of a sub-request becomes a `Warn` console entry.

## Out of scope

The script-side `pm.sendRequest` API (M3-22, M3-31), cookies jar, request scripts for sub-requests.

## Acceptance criteria

- [x] Tests with `FakeHttpTransport` and `FakeScriptEngine` whose function calls `invocation.Host.Send(...)`: the transport
      receives the resolved request (variables from the overlay, including one set earlier in the same block); the response
      is returned with the body text; failures are returned as `Error`; the 21st call is refused; sub-requests are not
      recorded as chain runs and produce network log entries; cancelling the token cancels an in-flight send promptly.
- [x] 0 warnings, XML docs, all tests green.

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

`feat(core): let scripts send extra requests through the transport`
