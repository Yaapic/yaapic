# M6-32: Infrastructure: HTTP/1.1 request reader and response writer

Status: done
Platform: any
Depends on: M6-29

## Goal

Read HTTP/1.1 requests from a stream and write responses, with the limits of ADR 0020 ("HTTP server"). Pure stream code, no socket,
so that it is fully unit-tested; the listener is M6-33.

## Design decisions

- Read first: ADR 0020, RFC 9112 (message syntax, chunked coding), `src/Yaapic.Infrastructure/Auth/TcpLoopbackRedirectReceiver.cs`
  (how the M4 loopback receiver reads a request line and headers), M6-29 (`MockHttpRequest`, `MockHttpResponse`).
- Folder `src/Yaapic.Infrastructure/Mocks/`:
  - `Http11Limits` (internal record): header bytes 65 536, headers 100, body 10 485 760, read timeout 30 s.
  - `Http11RequestReader` (internal sealed): constructor `(Stream stream, Http11Limits limits)`;
    `Task<Http11ReadResult> ReadAsync(CancellationToken ct)` with `Http11ReadResult` = one of `Request(MockHttpRequest, bool KeepAlive)`,
    `Closed` (clean end of stream before a request), `Error(int Status)` (400 malformed, 413 body too large, 431 headers too large,
    505 version other than `HTTP/1.0`/`HTTP/1.1`, 501 a transfer coding other than `chunked`). Rules: request line `METHOD SP target SP
    HTTP/x.y` CRLF (a bare LF is accepted); header lines `name: value` (value trimmed; obsolete line folding rejected with 400); body by
    `Content-Length` (one valid value, else 400) or `Transfer-Encoding: chunked` (chunk extensions ignored, trailers read and dropped);
    neither -> empty body; `Expect: 100-continue` -> write `HTTP/1.1 100 Continue\r\n\r\n` before reading the body. Keep-alive: HTTP/1.1
    unless `Connection: close`; HTTP/1.0 only with `Connection: keep-alive`. The reader buffers (a pooled 8 KiB buffer) and may read
    ahead into the next pipelined request (keep the leftover bytes for the next `ReadAsync`).
  - `Http11ResponseWriter` (internal static): `Task WriteAsync(Stream stream, MockHttpResponse response, bool keepAlive, DateTimeOffset now,
    CancellationToken ct)`: status line `HTTP/1.1 {status} {reason}`, the response headers except any `Content-Length`, `Transfer-Encoding`,
    `Connection`, `Date` or `Server` set by the route (those are ours), then `Content-Length` (the body length even when `OmitBody`),
    `Date` (RFC 1123), `Server: Yaapic-Mock`, `Connection: keep-alive` or `close`; then the body unless `OmitBody`. Header values with CR or
    LF are rejected by replacing them with a space. Status `204` and `304` never send a body or `Content-Length`.
  - `Http11ResponseWriter.WriteErrorAsync(Stream, int status, CancellationToken)`: a minimal `text/plain` response with `Connection: close`.

## Out of scope

Sockets and connection management (M6-33), TLS, HTTP/2.

## Acceptance criteria

- [x] Tests in `Yaapic.Infrastructure.Tests/Mocks/` over `MemoryStream`/a pipe: simple GET; POST with `Content-Length`; chunked body with
  extensions and trailers; `Expect: 100-continue` writes the interim response; two pipelined requests read in order; bare LF; each error
  status (malformed line, two different `Content-Length`, 101 headers, 70 KiB of headers, 11 MiB body, `HTTP/2.0`, `gzip` coding); keep-alive
  rules for 1.0 and 1.1; writer output exact for a JSON response, a HEAD response (`Content-Length` without body) and a 204; route headers
  `Content-Length`/`Connection` ignored; CR/LF in a header value neutralised.
- [x] Architecture tests green, 0 warnings, XML docs (internal types documented too if the project requires it), all tests green.

## Suggested commit message

`feat(infrastructure): read and write HTTP/1.1 messages for mock servers`
