# M1-07: Transport port and request sender

Status: done
Platform: any
Depends on: M1-02, M1-05, M1-06

## Goal

The use case "send this request now", with the HTTP transport behind a port.

## Design decisions

- `interface IHttpTransport { Task<SendResult> SendAsync(ResolvedRequest request, CancellationToken ct); }` in
  `Yaapic.Core.Http`. Contract: network problems, timeouts and cancellation are returned as `SendResult.Fail`, never thrown.
- `sealed record SendReport(SendResult Result, IReadOnlyList<string> UnresolvedVariables)`.
- `sealed class RequestSender(IHttpTransport transport)` with
  `Task<SendReport> SendAsync(HttpRequestDefinition request, EnvironmentDefinition? environment, CancellationToken ct)`:
  source = `new CompositeVariableSource(new EnvironmentVariableSource(environment), new DynamicVariableSource())`;
  resolve with `RequestResolver`; call the transport; return the report with `ResolvedRequest.Unresolved`.
- Unresolved variables never block the send (the UI warns, M1-18).
- Defensive: if the transport throws anyway, `OperationCanceledException` becomes `Fail(Canceled, "Request canceled")`
  and any other exception `Fail(Unknown, ex.Message)`.

## Out of scope

The real transport (M1-08), retries, history, scripts.

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests` with a fake `IHttpTransport` that captures the `ResolvedRequest`: environment
      variable substituted into the URL; `{{$guid}}` substituted; environment wins over a dynamic name it defines
      (`$guid` set in the environment); unresolved names surfaced; transport result passed through; transport exception
      -> `Unknown`; `OperationCanceledException` -> `Canceled`.
- [x] 0 warnings.

## Suggested commit message

`feat(core): add request sender and HTTP transport port`
