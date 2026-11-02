# M2-07: HttpClient transport: all methods, bodies, redirects and timeout

Status: todo
Platform: any
Depends on: M2-06

## Goal

`HttpClientTransport` sends every method and body kind and honours the per-request settings.

## Design decisions

- Method: map the seven `HttpMethodKind` values to `HttpMethod.Get/Post/Put/Patch/Delete/Head/Options` with a static
  switch (never `new HttpMethod(string)` from user text).
- Bodies: `BodyText` as today (`StringContent`, content type from `BodyContentType`); `Parts` ->
  `MultipartFormDataContent` (text part: `StringContent` added with its name; file part: `StreamContent` over
  `File.OpenRead`, added with its name and the file name, content type `application/octet-stream`); `BodyFilePath` ->
  `StreamContent` with `BodyContentType`. A file that cannot be opened returns
  `SendResult.Fail(SendFailureKind.FileUnreadable, "Cannot read file '<path>': <reason>")` before any network call. All
  streams are disposed with the message.
- Redirects: `DefaultHttpClient` gains `CreateNonRedirecting()` (same handler settings, `AllowAutoRedirect = false`).
  The `HttpClientTransport` constructor becomes `(HttpClient client, HttpClient noRedirectClient, TimeSpan timeout)`;
  `FollowRedirects == false` uses `noRedirectClient`. Update `AppComposition` and the existing tests that construct it.
- Timeout: `TimeoutSeconds > 0` replaces the default `timeout` for that request; the message
  `Request timed out after N s` uses the effective value.
- With redirects off, a 3xx response is returned like any other response (status, headers including `Location`).

## Out of scope

Cookies, proxy, TLS options, upload progress, the response side.

## Acceptance criteria

- [ ] Tests in `Yaapic.Infrastructure.Tests` against the loopback server (extend the helper only if it does not already
      echo method, headers and body bytes): each method arrives with the right verb; HEAD returns no body; urlencoded
      body; multipart with a text and a file part (boundary header present, file bytes intact); binary file bytes intact;
      unreadable file -> `FileUnreadable` with no request received; a 302 is followed by default and returned untouched
      when `FollowRedirects` is false; a per-request timeout of 1 s against a `/delay/3000` endpoint times out; the M1
      transport tests still pass.
- [ ] 0 warnings; AOT analyzers clean.

## Suggested commit message

`feat(infrastructure): send all methods and body kinds, honour redirect and timeout settings`
