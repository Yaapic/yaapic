# M4-38: Domain and YAML: HTTP version preference

Status: done
Platform: any
Depends on: M4-03

## Goal

A request can carry an HTTP version preference (`Default`, `Http1`, `Http2`, `Http3`), saved in its YAML file.

## Design decisions

- Read first: ADR 0014 (HTTP version), ADR 0004, `RequestSettings.cs` (positional constructor `(followRedirects, timeoutSeconds)` and
  `Default`), every call site of `new RequestSettings(` and the YAML mapping of `settings` in `RequestYamlSerializer.cs`.
- `public enum HttpVersionPreference { Default, Http1, Http2, Http3 }` in `Yaapic.Domain.Http` (XML docs: `Default` = HTTP/1.1 with
  upgrade to HTTP/2 through ALPN).
- `RequestSettings` gets a third constructor parameter `HttpVersionPreference httpVersion = HttpVersionPreference.Default` and the property
  `HttpVersion`; `RequestSettings.Default` unchanged semantically. Every existing call keeps compiling and behaving.
- `HttpRequestDefinition.IsEquivalentTo` (and the dirty-state comparison of the editor) must treat a different `HttpVersion` as a change:
  extend and test.
- YAML: `settings.httpVersion: default | http1 | http2 | http3`, written **only when not `default`** so existing files do not change; a missing
  or unknown value reads as `Default`.
- `docs/file-format.md` documents the key.

## Out of scope

Using it in the transport (M4-39), the editor view model and UI (M4-59, M4-69, M4-77).

## Acceptance criteria

- [x] Tests in `Yaapic.Domain.Tests` and `Yaapic.Infrastructure.Tests`: default value; round trip of each value; absent key reads as `Default`; a
  file with `Default` is byte-identical to the file written before this card (golden test on an existing fixture); unknown text; equivalence
  test.
- [x] Docs updated. Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(domain): add the HTTP version preference to request settings`
