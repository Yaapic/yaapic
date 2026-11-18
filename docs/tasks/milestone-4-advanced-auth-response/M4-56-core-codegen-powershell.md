# M4-56: Core: code generator, PowerShell (Invoke-RestMethod)

Status: done
Platform: any
Depends on: M4-50

## Goal

`PowerShellCodeGenerator` produces a runnable `Invoke-RestMethod` script for PowerShell 5.1 and 7.

## Design decisions

- Read first: ADR 0015, M4-50. `Id` `powershell`, `DisplayName` `PowerShell (Invoke-RestMethod)`, `EditorLanguage` `powershell`; 4-space indent.
- Shape:
  ```powershell
  $headers = @{
      "Accept" = "application/json"
  }
  $body = '{"a":1}'

  $response = Invoke-RestMethod -Uri 'https://api.example.com/users?id=1' -Method Post -Headers $headers -ContentType 'application/json' -Body $body
  $response
  ```
  - `-Method` uses the PascalCase name (`Get`, `Post`, `Put`, `Delete`, `Head`, `Options`, `Patch`); other names are passed as written.
  - `Content-Type` header goes to `-ContentType` (not into `$headers`); `$headers` is omitted when empty; duplicate names are merged with `, `.
  - Body: `Text` as a single-quoted string (here-string `@'` ... `'@` when the content has a newline); `Form` as `-Body @{ "a" = "1" }` (repeated names:
    not representable, fall back to the single-quoted urlencoded string and a `# repeated field names` comment); `Multipart` as `-Form @{ "name" = "value";
    "file" = Get-Item -Path '/tmp/report.pdf' }` (PowerShell 7 only: a first-line comment `# Requires PowerShell 7+`); `File` as `-InFile '/path'`; `None` has no
    body argument.
  - Single-quoted strings double the single quote (`''`). Double-quoted strings (only used for keys and values of the `$headers` table) escape the backtick,
    `"`, `$` with a backtick and newlines as `` `n ``.
- `AuthNote` as a `#` comment on the first line. Trailing newline.

## Out of scope

`Invoke-WebRequest`, `curl.exe` quoting for cmd, error handling.

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests`: exact expected text for every sample of `CodeSamples` (JSON with newline uses the here-string); escape helpers; method casing;
  `-ContentType` placement; repeated form field fallback.
- [x] A test, skipped when neither `pwsh` nor `powershell` is on the PATH, that parses each generated text with
  `[System.Management.Automation.Language.Parser]::ParseInput` and expects no errors.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the PowerShell code generator`
