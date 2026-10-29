# M1-09: Response body formatter (Pretty / Raw)

Status: todo
Platform: any
Depends on: M1-02

## Goal

Decode a response body to text and pretty-print JSON and XML, for the Pretty and Raw views.

## Design decisions

- Namespace `Yaapic.Core.Responses`, folder `src/Yaapic.Core/Responses/`.
- `enum BodyViewMode { Pretty, Raw }`.
- `sealed record FormattedBody(string Text, string Language, bool IsBinary, bool PrettyApplied)`. `Language` is one of
  `json`, `xml`, `html`, `text` (for a later code editor).
- `static class ResponseFormatter { public static FormattedBody Format(HttpResponseData response, BodyViewMode mode); }`.
- Language from the media type of `ContentType` (lowercase, parameters ignored): `application/json`, `text/json` or a
  `+json` suffix -> `json`; `application/xml`, `text/xml` or `+xml` -> `xml`; `text/html` -> `html`; anything else ->
  `text`. If there is no content type and the first non-whitespace byte is `{` or `[`, use `json`; `<`, use `xml`.
- Binary: media type starts with `image/`, `audio/`, `video/`, `font/`, or is `application/octet-stream`,
  `application/pdf`, `application/zip`, `application/gzip`; or the first 8000 bytes contain a `0x00` byte. Binary result:
  `Text = ""`, `IsBinary = true`, `PrettyApplied = false` (the UI shows "Binary, N bytes").
- Decoding: `charset` parameter of `ContentType`, via `Encoding.GetEncoding(name)` inside try/catch; unknown or missing
  -> UTF-8. A UTF-8 BOM is stripped.
- Raw mode: decoded text unchanged, `PrettyApplied = false`.
- Pretty mode, only when the body is at most 2 MiB (2 * 1024 * 1024 bytes), otherwise same as Raw:
  - `json`: `JsonDocument.Parse(text)` then `WriteTo` a `Utf8JsonWriter` with `Indented = true`, `IndentSize = 2`,
    `Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping` (keeps non-ASCII and `<` readable); numbers keep their text.
  - `xml`: `XmlReader` with `DtdProcessing = Prohibit`, `XmlResolver = null`, written to an `XmlWriter` with
    `Indent = true`, `IndentChars = "  "`, `OmitXmlDeclaration = true`, `NewLineChars = "\n"`.
  - `html` and `text`: unchanged.
  - Parse failure (`JsonException`, `XmlException`): return the raw text with `PrettyApplied = false`. Never throw.
- Line endings of pretty output are `\n`.

## Out of scope

HTML formatting, syntax colouring, search, images preview, JSONPath.

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests`: JSON pretty (nested object, array, unicode, big number kept verbatim); invalid JSON
      falls back; XML pretty; XML with a DOCTYPE falls back without resolving it; `+json` and `+xml` suffixes; sniffing
      without content type; `charset=iso-8859-1` decoding; BOM stripped; binary by media type and by NUL byte; Raw mode
      untouched; body above 2 MiB not pretty-printed.
- [ ] 0 warnings (AOT analyzers: no reflection-based `JsonSerializer` call).

## Suggested commit message

`feat(core): add response body formatter`
