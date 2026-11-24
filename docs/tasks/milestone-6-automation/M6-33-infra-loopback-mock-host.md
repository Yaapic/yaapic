# M6-33: Infrastructure: loopback HTTP host for mock servers

Status: todo
Platform: any
Depends on: M6-32

## Goal

Listen on `127.0.0.1:{port}`, accept connections and serve requests through a handler, with the connection limits of ADR 0020.

## Design decisions

- Read first: ADR 0020 ("HTTP server", "Ports"), M6-32, `src/Yaapic.Infrastructure/Auth/TcpLoopbackRedirectReceiver.cs` (listener start,
  port-in-use handling, disposal).
- Port in Core, `src/Yaapic.Core/Mocks/IMockHttpHost.cs`:
  ```csharp
  public interface IMockHttpHost
  {
      Task<IMockHttpServer> StartAsync(int port, Func<MockHttpRequest, CancellationToken, Task<MockHttpResponse>> handler, CancellationToken ct);
  }
  public interface IMockHttpServer : IAsyncDisposable { int Port { get; } }
  public sealed class MockPortInUseException(int port) : Exception(Strings.MockPortInUse(port)) { public int Port { get; } = port; }
  ```
- `src/Yaapic.Infrastructure/Mocks/TcpMockHttpHost.cs` (public sealed, implements `IMockHttpHost`, constructor `(TimeProvider time)`):
  - `TcpListener(IPAddress.Loopback, port)`, `ExclusiveAddressUse = true` on Windows; `SocketException` with `AddressAlreadyInUse` or
    `AccessDenied` -> `MockPortInUseException`.
  - An accept loop; each connection runs on its own task: a `Http11RequestReader` loop; per request, `await Task.Delay(response.Delay,
    time, ct)` after the handler, then `Http11ResponseWriter.WriteAsync`; close when `KeepAlive` is false, on a reader error (write the
    error status first) or after 30 s idle (`Http11Limits` read timeout through a linked `CancellationTokenSource.CancelAfter`).
  - At most 100 open connections: the 101st is answered `503` (`WriteErrorAsync`) and closed.
  - A handler exception answers `500` with `text/plain` "Mock server error" and keeps the connection only if the response could be written.
  - `DisposeAsync` stops the listener, cancels every connection and waits for their tasks (at most 5 s).
- String (EN and FR): `mock.portInUse` = "Port {0} is already in use.".

## Out of scope

Which definition answers (M6-34), the log.

## Acceptance criteria

- [ ] Tests in `Yaapic.Infrastructure.Tests/Mocks/TcpMockHttpHostTests.cs` with a real `HttpClient` against a free port (bind port 0 is not
  allowed by the contract: pick a free port with a temporary `TcpListener(IPAddress.Loopback, 0)` then release it): GET and POST reach the
  handler with headers and body; keep-alive reuses one connection for two requests (count accepted connections through an internal hook);
  delay applied (short); port in use throws `MockPortInUseException`; dispose stops answering and frees the port; a handler exception gives
  500; 101 concurrent idle connections: the last one gets 503.
- [ ] The server is reachable at `http://127.0.0.1:{port}` and not on another interface (connect to the machine's non-loopback address,
  if any, is refused; skip that assertion when the machine has none).
- [ ] No process or socket left open after the tests; architecture tests green, 0 warnings, XML docs, all tests green; no IL2xxx/IL3xxx.

## Suggested commit message

`feat(infrastructure): serve mock servers on the loopback interface`
