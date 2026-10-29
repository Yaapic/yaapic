# M1-08: HttpClient transport and Infrastructure project

Status: todo
Platform: any
Depends on: M1-07

## Goal

Create `src/Yaapic.Infrastructure` (adapters for the Core ports) and implement `IHttpTransport` on `HttpClient`.
Creates `tests/Yaapic.Infrastructure.Tests`.

## Design decisions

- `Yaapic.Infrastructure.csproj`: `net10.0`, references `Yaapic.Core` and `Yaapic.Domain`. In `DependencyRulesTests`,
  add `["Yaapic.Infrastructure"] = ["Yaapic.Core", "Yaapic.Domain"]`. Add both new projects to `Yaapic.sln`; the test
  project mirrors `Yaapic.Architecture.Tests.csproj` and references `Yaapic.Infrastructure`, `Yaapic.Core`, `Yaapic.Domain`.
- `sealed class HttpClientTransport(HttpClient client, TimeSpan timeout) : IHttpTransport` in
  `Yaapic.Infrastructure.Http`.
- `static class DefaultHttpClient { public static HttpClient Create(); }`: `SocketsHttpHandler` with
  `AllowAutoRedirect = true`, `MaxAutomaticRedirections = 10`, `AutomaticDecompression = DecompressionMethods.All`,
  `UseCookies = false`; `HttpClient.Timeout = Timeout.InfiniteTimeSpan` (the transport owns the timeout). The default
  transport timeout used by the composition root (M1-23) is 30 seconds.
- Request: `Uri.TryCreate(url, UriKind.Absolute, out var uri)` must succeed with scheme `http` or `https`, otherwise
  return `Fail(InvalidUrl, "Invalid URL: <url>")` without calling the handler. Method `HttpMethod.Get` / `HttpMethod.Post`.
  Content when `BodyText` is not null: `new StringContent(BodyText, Encoding.UTF8)`, then replace its `Content-Type` with
  `BodyContentType` (`MediaTypeHeaderValue.TryParse`; if parsing fails, `TryAddWithoutValidation`). Headers: skip
  `Content-Length` and `Host`; `request.Headers.TryAddWithoutValidation`; if that returns false and there is a content,
  `content.Headers.TryAddWithoutValidation`; otherwise drop the header.
- Timeout: linked `CancellationTokenSource` with `CancelAfter(timeout)`. Mapping: caller token canceled -> `Canceled`
  ("Request canceled"); canceled by the timeout -> `Timeout` ("Request timed out after N s"); `HttpRequestException` ->
  `ConnectionFailed` with the innermost exception message; any other exception -> `Unknown`.
- Response: `HttpCompletionOption.ResponseHeadersRead`, then `ReadAsByteArrayAsync`. Headers = response headers then
  content headers, one `KeyValueEntry` per value. `ReasonPhrase ?? ""`. `FinalUrl = response.RequestMessage!.RequestUri!.AbsoluteUri`.
  `Duration` measured with `Stopwatch` from just before `SendAsync` to after the body is read.

## Out of scope

Proxy, TLS options, client certificates, HTTP version selection (M4), streaming, cookies, response size cap.

## Acceptance criteria

- [ ] Tests with a stub `HttpMessageHandler` (no network): GET maps status, reason, response and content headers, body,
      final URL; POST sends the body bytes with the expected `Content-Type`; user headers reach the request; a
      `Content-Type` override is applied; invalid URL and `ftp://` -> `InvalidUrl` with the handler never called;
      `HttpRequestException` -> `ConnectionFailed`; handler slower than a 50 ms timeout -> `Timeout`; caller cancellation
      -> `Canceled`; a 404 is `IsSuccess`.
- [ ] 0 warnings (AOT analyzers on), architecture tests green with the new rule.

## Suggested commit message

`feat(infrastructure): add HttpClient transport`
