# Response tools

Tools of the response panel and of the request: timings, HTTP version, client certificates, diff, code generation and the JWT
decoder. Decisions: [ADR 0014](adr/0014-transport-certificates-timings.md) and
[ADR 0015](adr/0015-response-diff-and-code-generation.md).

## Timing tab

Each response shows the protocol used (for example `HTTP/2`) and a **Timing** tab with the phases of the request. The total is the
duration shown in the status line.

| Phase | Meaning |
| --- | --- |
| DNS lookup | Resolving the host name. |
| TCP connect | Opening the socket. |
| TLS handshake | From the end of the connect to the end of the handshake (an approximation); not shown for plain `http`. |
| Waiting (TTFB) | From the end of the connection phases (or of the send) to the response headers. |
| Download | From the headers to the end of the body. |

- **Connection reused**: when the connection comes from the pool (HTTP/1.1 keep-alive or HTTP/2), no new connection is made, so
  DNS, connect and TLS show `n/a` and the tab says "Connection reused" (not 0 ms).
- **Redirects**: the phases of the last hop are shown, with the total elapsed time.
- **Proxy**: behind a proxy DNS, connect and TLS cannot be timed (`n/a`); waiting and download are still measured.
- Timings are not stored in the history.

## HTTP version

Request settings, "HTTP version": automatic (HTTP/1.1 with an upgrade to HTTP/2 through ALPN), HTTP/1.1, HTTP/2 or HTTP/3
(experimental). HTTP/3 is unavailable when the machine has no QUIC support and falls back to a lower version when the server
does not speak it. The file key is `settings.httpVersion`.

## Client certificates (mTLS)

Palette: "Client certificates...". Each entry has a name, a host pattern (`host`, `*.domain` or either with `:port`; `https`
only; the most specific match wins), a format (PFX, or PEM with an optional separate key file) and an optional passphrase,
stored in the secret vault. The list lives in `<user data dir>/Yaapic/certificates.json`, outside the workspace. A locked vault
makes sends to hosts that need a certificate fail with a message asking to unlock it; other hosts are unaffected. Server
validation stays the system default (no custom CA or "skip verification" yet).

## Diff tab

Compares the current response with the **previous response of the same tab**, or with a **pinned baseline** ("Pin as baseline",
"Unpin baseline"). Both are kept in memory only; the history stores no bodies, so a diff against a history entry is not possible.
Send the request twice to get two responses.

- Bodies go through the response formatter first (JSON is pretty printed, key order kept), then a line diff. The option "Ignore
  whitespace" ignores leading and trailing whitespace of lines. Long unchanged runs collapse to 3 lines of context ("Show
  unchanged lines" expands them).
- Headers are compared by name, ignoring case: added, removed, changed. "Ignore volatile headers" (on by default) hides `Date`,
  `Age`, `X-Request-Id`, `X-Amzn-Trace-Id`, `CF-Ray` and `Server-Timing`.
- Only text bodies up to 1 MiB are compared; others show "Too large or binary to compare".

## Generate code

Palette: "Generate code for this request". Languages: cURL (bash), JavaScript (`fetch`), Python (`requests`), C#
(`HttpClient`), PowerShell (`Invoke-RestMethod`) and Go (`net/http`). The code is the request as it would be sent, with variables
resolved and the headers of Basic, Bearer and API key auth; unresolved variables stay as `{{name}}`. OAuth 2.0 and JWT requests get
an `Authorization: Bearer <access token>` placeholder, Digest and AWS Signature v4 requests get a comment saying the caller
must compute the signature. File bodies are read from their path.

**The generated text can contain secrets** (secret variables are resolved like in a send): the dialog shows a warning, and
nothing is written to disk, the text is only shown and copied. cURL, PowerShell and Go are shown as plain text (the bundled
editor has no grammar for them); JavaScript, Python and C# are highlighted.

## JWT decoder

Palette: "Decode JWT". Paste a token to see the header and payload and the status of `exp`, `nbf` and `iat`. The **signature is
not verified**, and the decoder never runs automatically.
