# M4-24: Infrastructure: loopback redirect receiver

Status: done
Platform: any
Depends on: M4-23

## Goal

The real `ILoopbackRedirectReceiver`: a `TcpListener` on `127.0.0.1` that captures the OAuth redirect.

## Design decisions

- Read first: ADR 0013 (Authorization-code redirect), M4-23, `tests/Yaapic.Infrastructure.Tests/EndToEnd/LoopbackServer.cs` (test helper style).
- `src/Yaapic.Infrastructure/Auth/TcpLoopbackRedirectReceiver.cs`, `public sealed class`, implements the port.
- Binds `IPAddress.Loopback` only (never `Any`), the given port; a bind failure (`SocketException` address in use) throws
  `InvalidOperationException(Strings.OAuth2PortBusy(port))`.
- Accepts connections in a loop until one asks for `path` (GET, path equal ignoring a trailing slash); other requests (for example
  `/favicon.ico`) get `404` and the loop continues. Reads the request line and headers with a 8 KiB cap and a 5 s read timeout per
  connection, parses `METHOD SP target SP HTTP/x`; anything malformed gets `400`.
- Replies `200 OK`, `Content-Type: text/html; charset=utf-8`, `Connection: close`, `Content-Length`, with `successHtml`, then closes the socket and
  the listener, and returns the request target.
- The `Host` header must be `127.0.0.1:<port>` or `localhost:<port>`; other values get `400` (DNS rebinding guard).
- Honors the cancellation token (stops the listener promptly, throws `OperationCanceledException`).
- String `OAuth2PortBusy`, EN and FR.
- No new package, AOT safe (sockets only).

## Out of scope

HTTPS, a fixed set of ports, the browser.

## Acceptance criteria

- [x] Tests in `Yaapic.Infrastructure.Tests` (real sockets on a free port found by binding port 0 first): returns the target after a GET with a
  query; ignores `/favicon.ico` then succeeds; wrong `Host` gets 400 and is ignored; oversize and malformed requests get 400; cancel
  stops the listener and frees the port (a second bind works); port in use gives the localized error; the response has the HTML and
  correct `Content-Length`.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green; no listener left open after the tests.

## Suggested commit message

`feat(infra): add the loopback redirect receiver for OAuth 2.0`
