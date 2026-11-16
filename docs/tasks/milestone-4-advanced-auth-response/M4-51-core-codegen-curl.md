# M4-51: Core: code generator, cURL

Status: todo
Platform: any
Depends on: M4-50

## Goal

`CurlCodeGenerator` produces a runnable `curl` command for bash and zsh.

## Design decisions

- Read first: ADR 0015 (Style), M4-50 (`CodeRequest`, `CodeSamples`, `CodeText`). File `src/Yaapic.Core/CodeGeneration/CurlCodeGenerator.cs`;
  `Id` `curl`, `DisplayName` `cURL`, `EditorLanguage` `shell`.
- Layout: first line `curl --request <METHOD> '<url>'` for every method except `GET`, which is written `curl '<url>'`, then one option per line, continued with ` \` and a 2-space indent:
  `--header 'Name: value'` per header; body: `Text` as `--data-raw '<content>'`; `Form` as one `--data-urlencode 'name=value'` per field; `Multipart` as one
  `--form 'name=value'` or `--form 'name=@/path'` per part; `File` as `--data-binary '@/path'`; `HEAD` uses `--head` instead of `--request HEAD`.
- Quoting: single quotes; an embedded single quote becomes `'\''`. Newlines inside values stay literal inside the single quotes. `AuthNote` is emitted as a
  first line comment `# <note>`.
- Multipart text parts whose value starts with `@` or `<` would be read as files by curl: emit them with `--form-string 'name=value'`.
- Trailing newline, no trailing backslash on the last line.

## Out of scope

Windows cmd quoting (PowerShell has its own generator, M4-56), `--compressed`, `--location` (the redirect setting is not part of `CodeRequest`).

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests`: the exact expected text, written by hand in the test, for every sample of `CodeSamples` (including awkward quotes, the
  JSON body with a newline, multipart with a file, the unresolved URL, the digest note); `HEAD`; a `Multipart` text value starting with `@`.
- [ ] A test, skipped when `curl` is not on the PATH, that runs the generated command for `GetSimple` against a local `TcpListener` HTTP server and checks the
  request line and headers received.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the cURL code generator`
