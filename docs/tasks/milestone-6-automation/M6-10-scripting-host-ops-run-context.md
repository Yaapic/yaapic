# M6-10: Scripting: host operations for iteration data and execution control

Status: todo
Platform: any
Depends on: M6-09

## Goal

Expose the M6-09 host members to both engines through the `__host(op, argsJson)` bridge, before the preludes use them.

## Design decisions

- Read first: `src/Yaapic.Scripting/HostOps.cs`, `HostBridge.cs`, `HostDtos.cs` (`HostJsonContext`), ADR 0009 (host protocol),
  M6-09, `tests/Yaapic.Scripting.Tests/HostBridgeTests.cs`.
- New operations in `HostOps`: `IterGet = "iter.get"` (no arguments; returns the data row as `KeyValueDto[]`, in order),
  `ExecNext = "exec.next"` (arguments `{"value": string|null}`; calls `host.SetNextRequest(value)`; returns `null`),
  `ExecSkip = "exec.skip"` (no arguments; calls `host.SkipRequest()`; returns `null`; the `InvalidOperationException` of a
  post-response call becomes the usual `{"err": message}` envelope).
- New DTO `internal sealed record NextArgs(string? Value);` registered in `HostJsonContext` (source generation only).
- `InfoDto` gains `int Iteration` and `int IterationCount` (appended); `info.get` fills them from `host.Info`.
- Keep `HostOps.Unsupported` for the members that stay unsupported.

## Out of scope

The JavaScript and Lua prelude changes (M6-11, M6-12).

## Acceptance criteria

- [ ] `HostBridgeTests`: `iter.get` returns the row in order and `[]` outside a run; `exec.next` with a name and with `null`
  reaches the host; `exec.skip` in pre-request sets the flag and in post-response returns the error envelope with the M6-09 message;
  `info.get` contains `iteration` and `iterationCount`.
- [ ] The AOT smoke check of `Yaapic.Scripting` used by M3 (see `docs/dependencies.md` / M3-16 notes) still passes; 0 IL2xxx/IL3xxx.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(scripting): add host operations for iteration data and execution control`
