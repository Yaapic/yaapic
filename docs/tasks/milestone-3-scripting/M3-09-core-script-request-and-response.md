# M3-09: Core: script request and response snapshots, cookies

Status: done
Platform: any
Depends on: M3-07

## Goal

Replace the placeholder `ScriptRequest`, `ScriptResponse` and `ScriptCookie` of M3-07 with the real types: the mutable
request a pre-request script edits, the read-only response a post-response script reads, and the `Set-Cookie` parser.

## Design decisions

- Read first: `src/Yaapic.Core/Http/QueryStringSync.cs` (M2-09), `RequestResolver.cs`, `Yaapic.Domain/Http/RequestBody.cs`,
  `HttpResponseData.cs`.
- `public sealed class ScriptRequest`: `string Method` (upper case text), `string Url` (the **full** URL: the definition `Url`
  with the enabled params appended through `QueryStringSync`, variables untouched), `List<KeyValueEntry> Headers`,
  `string BodyMode` (`none`, `raw`, `urlencoded`, `formdata`, `binary`), `string BodyText` (raw), `string BodyContentType`
  (raw content type text such as `json`), `List<KeyValueEntry> UrlEncoded`, `IReadOnlyList<KeyValueEntry> FormData`
  (read-only), `bool IsReadOnly` (set for post-response scripts; the setters and list mutators throw
  `InvalidOperationException("pm.request is read-only in post-response scripts")`; expose lists as an
  `ObservableList`-free small wrapper or check in the host op, your choice, but cover it with a test).
  - `static ScriptRequest FromDefinition(HttpRequestDefinition definition, bool readOnly)`.
  - `HttpRequestDefinition ApplyTo(HttpRequestDefinition original)` returns a new definition: method parsed (unknown method
    text -> `ScriptException`-style `InvalidOperationException("Unknown HTTP method 'x'")`), URL split back into `Url` +
    `QueryParams` with `QueryStringSync`, headers replaced, body updated for `raw` (text and content type) and
    `urlencoded` only; `none`/`formdata`/`binary` bodies are untouched. Auth and settings untouched (`pm.request.auth` is not
    supported in v1).
  - `static ScriptRequest FromOptions(...)` is **not** needed; `pm.sendRequest` builds a `ScriptRequest` from the script
    side through the same properties (M3-13).
- `public sealed record ScriptResponse(int Code, string Status, IReadOnlyList<KeyValueEntry> Headers, string BodyText,
  long SizeBytes, long ResponseTimeMs, string FinalUrl, IReadOnlyList<ScriptCookie> Cookies)`:
  `static ScriptResponse FromData(HttpResponseData data)`: `BodyText` decoded with the `charset` of `Content-Type` (UTF-8
  when absent or unknown; invalid bytes replaced), empty for binary content types (`image/*`, `application/octet-stream`,
  `application/pdf`, `audio/*`, `video/*`, `application/zip`), `ResponseTimeMs` = duration rounded.
- `public sealed record ScriptCookie(string Name, string Value, string? Domain, string? Path, DateTimeOffset? Expires,
  bool HttpOnly, bool Secure, string? SameSite)` and `public static class SetCookieParser` with
  `IReadOnlyList<ScriptCookie> Parse(IEnumerable<KeyValueEntry> responseHeaders)`: one cookie per `Set-Cookie` header,
  attributes case-insensitive (`Expires` RFC 1123 via `DateTimeOffset.TryParse`, `Max-Age` seconds -> `Expires` relative to
  now is **not** computed: keep `Expires` null and ignore `Max-Age` for v1, note it), quoted values unquoted, malformed
  headers skipped. No cookie jar, nothing stored.

## Out of scope

Applying scripts, pm bindings, auth mutation, response streaming.

## Acceptance criteria

- [x] Tests: `FromDefinition`/`ApplyTo` round trip is lossless for GET/POST raw JSON/urlencoded; params appended to and
      split from the URL; changing method, URL, a header, the raw body; unknown method rejected; read-only request throws on
      every mutation; form-data and binary bodies survive `ApplyTo`; response charset handling (utf-8, latin-1, none),
      binary content type -> empty text; `SetCookieParser` cases (flags, expires, quoted, two cookies, malformed).
- [x] The M3-07 placeholders are gone; 0 warnings, XML docs, all tests green.

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

`feat(core): add script request, response and cookie snapshots`
