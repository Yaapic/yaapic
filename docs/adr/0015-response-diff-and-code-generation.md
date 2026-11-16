# ADR 0015: Response diff and code generation

Status: Accepted

See also: [ADR 0002](0002-code-editor.md), [ADR 0005](0005-storage-format.md), [ADR 0010](0010-secret-vault.md),
[ADR 0013](0013-advanced-authentication.md). The diff and code generation cards of milestone 4 read this ADR first.

## Context

Milestone 4 adds a response diff (what changed between two sends) and a "generate code" dialog that turns a request
into runnable source in several languages. Both are pure text transformations that belong in `Yaapic.Core`, so their
contracts are fixed here.

## Decision

### Response diff

- **Subject.** The current response of a request tab against (a) the previous response of the same tab, or (b) a
  baseline the user pinned. Both live in memory only (`ResponseSnapshot`: status, headers, body text, `At`). The history
  stores no response bodies, so a diff against a history entry is impossible and out of scope. A snapshot is taken only
  for text bodies up to 1 MiB; bigger or binary bodies show "Too large or binary to compare".
- **Body comparison.** Both sides go through the existing `ResponseFormatter` (JSON pretty-printed, key order preserved
  because order is data), then a line diff (Myers, `O(ND)`). Option: ignore leading and trailing whitespace of lines
  (default off). The output is a flat list of `DiffLine(Op, Text, OldLine, NewLine)` with `Op` in `Equal`, `Added`,
  `Removed`; the view model collapses long equal runs to 3 lines of context.
- **Header comparison.** By name, case-insensitive: added, removed, changed. Option "ignore volatile headers" (default
  **on**) hides `Date`, `Age`, `X-Request-Id`, `X-Amzn-Trace-Id`, `CF-Ray`, `Server-Timing`.

### Code generation

- **Contract.** `ICodeGenerator { string Id; string DisplayName; string EditorLanguage; string Generate(CodeRequest) }`
  in `Yaapic.Core.CodeGeneration`. Generators are pure and deterministic and never touch the network or files.
  `CodeRequest` is built from a `ResolvedRequest`: method, URL, headers, and a body that is one of `None`,
  `Text(content, contentType)`, `Form(fields)` (url-encoded), `Multipart(parts)` (text or file path) or `File(path)`.
- **Content.** The request as it would be sent after variable resolution, including the headers produced by Basic, Bearer
  and API key auth. Unresolved variables stay as `{{name}}`. **Secret variables are resolved like in a send, so the
  generated text can contain secrets**: the dialog shows a one-line warning and nothing is written to disk. OAuth 2.0
  and JWT requests get an `Authorization: Bearer <access token>` placeholder header; Digest and AWS Signature v4 requests
  get a comment saying the signature must be computed by the caller.
- **Targets of milestone 4.** cURL (bash), JavaScript (`fetch`), Python (`requests`), C# (`HttpClient`), PowerShell
  (`Invoke-RestMethod`), Go (`net/http`). More languages are the blocked card M4-55.
- **Style.** Idiomatic and copy-paste runnable; 2-space indent for JavaScript, 4 for the others, tabs for Go; a trailing
  newline; string escaping specific to each language (a tested `Escape` helper per generator); file bodies read from
  their path.
- **Native AOT.** String building only; no template engine, no reflection.

### Editor and grammar check

The dialog shows the text in the existing read-only `CodeEditorView`; the editor language comes from `EditorLanguage`.
Grammars bundled in LoomEditor.TextMate 0.6.0 (the version pinned in `Directory.Packages.props`, checked against its
`NOTICE.md`):

| `EditorLanguage` | Grammar in 0.6.0 | Result |
| --- | --- | --- |
| `javascript` | yes | highlighted (already mapped) |
| `python` | yes | highlighted, needs a mapping |
| `csharp` | yes | highlighted, needs a mapping |
| `shell` | no | plain text |
| `powershell` | no | plain text |
| `go` | no | plain text |

`CodeEditorView.ApplyLanguage` (both App projects) maps only `json`, `xml`, `javascript` and `lua` today; the editor card
adds `python` and `csharp`, and every other id falls back to `plaintext` as it already does. A later LoomEditor release
with more grammars only needs a new mapping line.

## Out of scope

Importing from cURL (milestone 5), SDK-style clients, generating a whole collection, diffing against history entries.
