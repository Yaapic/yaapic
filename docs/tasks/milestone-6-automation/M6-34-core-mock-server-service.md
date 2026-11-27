# M6-34: Core: mock server service

Status: done
Platform: any
Depends on: M6-28, M6-30, M6-33

## Goal

Load, save, start and stop mock servers and keep their request logs (ADR 0020, "Ports", "Log", "UI").

## Design decisions

- Read first: ADR 0020, M6-27 to M6-33, `tests/Yaapic.TestDoubles/` (add a fake host here).
- `src/Yaapic.Core/Mocks/MockServerService.cs` (public sealed, `IAsyncDisposable`), constructor `(IMockServerStore store, IMockHttpHost host,
  TimeProvider time)`:
  - `Task LoadAsync(CancellationToken ct)`; `IReadOnlyList<MockServerDefinition> Servers` (store order, kept in memory).
  - `Task<IReadOnlyList<MockValidationIssue>> SaveAsync(MockServerDefinition server, CancellationToken ct)`: validates
    (`MockServerValidator`); with issues nothing is saved and they are returned; otherwise stores, replaces the in-memory definition
    (running servers read it at their next request) and, when the server runs and the port changed, restarts it on the new port
    (a failure leaves it stopped and is rethrown as `MockPortInUseException`). Two servers may share a port in their files; only
    starting one while another server of this service runs on that port fails (`MockPortInUseException`, checked before calling the host).
  - `Task DeleteAsync(Guid id, CancellationToken ct)`: stops it if running, deletes it, drops its log.
  - `Task StartAsync(Guid id, CancellationToken ct)` (no-op when running; validates first and throws `InvalidOperationException` with
    `Strings.MockInvalid` when there are issues; `MockPortInUseException` passes through), `Task StopAsync(Guid id)`, `bool IsRunning(Guid id)`,
    `int RunningCount`, `Task StopAllAsync()`; `DisposeAsync` = `StopAllAsync`.
  - The handler: `start = time.GetTimestamp()`, `MockResponder.Respond(current definition, request)`, then log
    `MockLogEntry.From(request, response, routeName, time.GetUtcNow(), elapsed incl. delay)` and return the response.
  - Log: per server a ring buffer of the last 500 entries (`lock`); `IReadOnlyList<MockLogEntry> GetLog(Guid id)` (newest first),
    `void ClearLog(Guid id)`.
  - Events (raised on the thread that caused them; subscribers marshal): `ServersChanged` (load, save, delete), `RunningChanged`
    (`EventHandler<Guid>`), `RequestLogged` (`EventHandler<MockLogged>` with `MockLogged(Guid ServerId, MockLogEntry Entry)`).
- `tests/Yaapic.TestDoubles/FakeMockHttpHost.cs`: records started ports, lets a test invoke the handler directly
  (`Task<MockHttpResponse> SendAsync(int port, MockHttpRequest request)`), can be told that a port is in use.
- Strings (EN and FR): `mock.invalid` = "This mock server has errors; fix them before starting it.".

## Out of scope

The view model (M6-35), composition (M6-42).

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests/Mocks/MockServerServiceTests.cs` with `InMemoryMockServerStore`, `FakeMockHttpHost` and `ManualTimeProvider`:
  load; save with issues returns them and stores nothing; save of a running server is used by the next request; port change restarts;
  start/stop/IsRunning/RunningCount and events; start of an invalid server throws; port in use passes through; delete stops and removes;
  log keeps 500 newest-first entries and records the route name or null; `StopAllAsync`.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the mock server service`
