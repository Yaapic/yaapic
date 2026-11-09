# M3-12: Core: post-response stage, test results and network console line

Status: done
Platform: any
Depends on: M3-11

## Goal

After a successful transport result, run the post-response blocks with the response, collect tests and keep the
response even when a script fails.

## Design decisions

- Read first: M3-11, ADR 0009 (error policy), `src/Yaapic.Domain/Http/SendResult.cs` (the success/failure shape).
- Post-response stage runs only when `Report.Result.IsSuccess` is true (a transport failure has no response: no post
  script runs, `Runs` keeps the pre-request runs only). For each chain step with a non-empty `PostResponse` block, in
  order: a **new** `ScriptHost` per step sharing the same overlay (variables written by the collection test script are
  visible to the request test script), `ScriptRequest` = `FromDefinition(sentDefinition, readOnly: true)` (the definition
  after pre-request mutation), `ScriptResponse.FromData(result.Response)`, `Info.EventName = "test"`,
  `Origin = "post-response · <step origin>"`, `ResponseCookies` from the response.
- A failing post-response run does **not** stop the chain (unlike pre-request): the next block still runs; the report keeps
  `Report` intact and every run (status per run). `AbortedBy` stays null; the view model shows each failed run's error next
  to the response. `Canceled`/`TimedOut` of one block likewise do not stop later blocks except when the token itself is
  cancelled (then stop and add a `Canceled` run).
- `Changes` of a post-response stage are kept (they are valid even when a later block failed); when a **failed** post block
  changed variables before failing, those changes are kept too (document the asymmetry with pre-request in XML docs).
- Network console line: the sender appends to `Runs`-independent output `ConsoleEntry(Source.Network, Info or Error,
  "<METHOD> <final url>   <status> · <ms> ms · <size>")` for the main request into a new report member
  `IReadOnlyList<ConsoleEntry> NetworkLog` (computed after the send; failure -> `Error` level with the failure text; the
  unresolved variable names -> one extra `Warn` system entry "Unresolved variables: a, b"). The console view model (M3-41)
  merges `NetworkLog` and the runs' console by time. Size text uses the same formatter as the response panel
  (`ResponseFormatter`/`SizeText` helper: reuse, do not duplicate; if it only exists in ViewModels, move the pure formatting
  function to `Yaapic.Core.Responses` and update the ViewModel to call it).
- Variable `Changes` from both stages are merged in order (collapsed per scope+name, last wins).

## Out of scope

`pm.sendRequest` (M3-13), persistence of changes (M3-44), UI.

## Acceptance criteria

- [x] Tests (fakes): post blocks run in chain order after a 200 and see response, request and shared variables; a failing
      post block keeps the response and runs the next block; transport failure runs no post block; a test result list
      aggregates in order across blocks; changes of both stages merged and collapsed; read-only request in post (mutation
      reports a failed run through the fake host contract test, see M3-09); network log text for 200, 404, failure and
      unresolved variables; cancellation between blocks.
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

`feat(core): run post-response scripts and collect tests`
