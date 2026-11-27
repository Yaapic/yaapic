# M6-17: Core: runner flow control (setNextRequest, skipRequest, loop guard)

Status: done
Platform: any
Depends on: M6-16

## Goal

Honour `yaapic.execution.setNextRequest` and `skipRequest` inside `CollectionRunner` exactly as ADR 0018 says.

## Design decisions

- Read first: ADR 0018 ("Script API"), M6-09 (`ScriptedSendReport.NextRequestSet`, `NextRequest`, `Skipped`), M6-16.
- In `CollectionRunner.RunAsync`, each iteration walks an index over `plan.Items` instead of a `foreach`:
  - after a step, when `report.NextRequestSet`:
    - `NextRequest` is `null`: end the run with status `Stopped` after this request (no further iteration);
    - otherwise find the target: first item whose `Request.Id.ToString("D")` equals the text (ordinal, ignoring case), else the
      first item whose `Request.Name` equals it (ordinal); found: continue the **same iteration** at that index; not found: end the run
      with `Failed` and `Error = Strings.RunnerNextRequestNotFound(text)`;
  - otherwise continue with the next index; past the last item the iteration ends.
  - A `Skipped` outcome is recorded (with no status code) and the walk continues; a skipped step still honours `setNextRequest`
    set by its pre-request scripts.
  - Loop guard: more than 1000 steps in one iteration end the run with `Failed` and `Strings.RunnerLoopGuard`.
  - Stop on failure ignores `Skipped`.
- `RunProgress.Total` stays `Iterations * Items.Count` (an estimate with jumps); `Completed` never exceeds it in the progress (clamp).
- Strings (EN and FR): `runner.nextRequestNotFound` = "setNextRequest: no request named '{0}' in this run";
  `runner.loopGuard` = "The run was stopped: more than 1000 requests in one iteration (setNextRequest loop?)".

## Out of scope

Engine preludes (done in M6-11, M6-12), view models.

## Acceptance criteria

- [x] Tests (same doubles as M6-16, scripts simulated by `FakeScriptEngine` replies calling `host.SetNextRequest`/`SkipRequest`):
  jump forward (skips the items in between), jump back once (with a counter in a variable) then continue; jump by id (any case);
  `null` stops the run with `Stopped` and no next iteration; unknown name gives `Failed` with the message; an infinite loop stops at
  1001 steps with the guard message; a skipped request is recorded and the next one runs; a skip plus `setNextRequest` in the same
  pre-request jumps; stop on failure does not stop on a skip.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): support setNextRequest and skipRequest in the runner`
