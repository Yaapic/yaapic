# M4-04: ADR 0015: response diff and code generation

Status: done
Platform: any
Depends on: none

## Goal

Write ADR 0015, the reference for the diff and code generation cards. Docs only. The decisions below are final.

## Design decisions (to record in the ADR)

- **Diff subject.** The current response of a request tab against (a) the previous response of the same tab, or (b) a baseline the user
  pinned. Both are kept in memory only (`ResponseSnapshot`: status, headers, body text, `At`); the history stores no response bodies, so a
  diff against a history entry is not possible and is out of scope. A snapshot is taken only for text bodies up to 1 MiB; bigger or binary
  bodies show "Too large or binary to compare".
- **Body comparison.** Both sides go through the existing `ResponseFormatter` (JSON pretty-printed, key order preserved because order
  is data), then a line diff (Myers, `O(ND)`). Option: ignore leading and trailing whitespace of lines (default off). Output is a flat
  list of `DiffLine(Op, Text, OldLine, NewLine)` with `Op` in `Equal`, `Added`, `Removed`; the view model collapses long equal runs to
  3 lines of context.
- **Header comparison.** By name, case-insensitive: added, removed, changed. Option "ignore volatile headers" (default **on**) hides
  `Date`, `Age`, `X-Request-Id`, `X-Amzn-Trace-Id`, `CF-Ray`, `Server-Timing`.
- **Code generation contract.** `ICodeGenerator { string Id; string DisplayName; string EditorLanguage; string Generate(CodeRequest) }`
  in `Yaapic.Core.CodeGeneration`; generators are pure and deterministic and never touch the network or files. `CodeRequest` is built
  from a `ResolvedRequest`: method, URL, headers, and a body that is one of `None`, `Text(content, contentType)`, `Form(fields)`
  (url-encoded), `Multipart(parts)` (text or file path) or `File(path)`.
- **What the generated code contains.** The request as it would be sent after variable resolution, including the headers produced by Basic,
  Bearer and API key auth. Unresolved variables stay as `{{name}}`. **Secret variables are resolved like in a send, so the generated text
  can contain secrets**: the dialog shows a one-line warning and nothing is written to disk. OAuth 2.0 and JWT requests get an
  `Authorization: Bearer <access token>` placeholder header; Digest and AWS Signature v4 requests get a comment saying the signature must be
  computed by the caller.
- **Targets of milestone 4**: cURL (bash), JavaScript (`fetch`), Python (`requests`), C# (`HttpClient`), PowerShell
  (`Invoke-RestMethod`), Go (`net/http`). More languages are the blocked card M4-55.
- **Style.** Idiomatic and copy-paste runnable, 2-space indent for JavaScript and 4 for the others (tabs for Go), a trailing newline,
  string escaping specific to each language (a tested `Escape` helper per generator), file bodies read from their path.
- **Editor.** The dialog shows the text in the existing read-only `CodeEditorView`; the editor language comes from `EditorLanguage`
  (`shell`, `javascript`, `python`, `csharp`, `powershell`, `go`); a language the editor has no grammar for shows plain text (check each
  grammar in LoomEditor and record the result in the ADR).
- **Native AOT.** String building only; no template engine, no reflection.

## Out of scope

Code, UI, importing from cURL (M5), SDK-style clients, generating a whole collection.

## Acceptance criteria

- [x] ADR 0015 (`docs/adr/0015-response-diff-and-code-generation.md`) contains every decision above plus the grammar check; the ADR
  index lists it.
- [x] No code changed; the build still has 0 warnings.

## Suggested commit message

`docs(adr): add ADR 0015 response diff and code generation`
