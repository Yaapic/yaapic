# M5-17: Core: cURL command tokenizer

Status: done
Platform: any
Depends on: M5-04

## Goal

Split pasted text into `curl` commands and each command into shell words, following bash quoting, so M5-18 only deals with options.

## Design decisions

- Read first: ADR 0017 (cURL), `src/Yaapic.Core/CodeGeneration/CurlCodeGenerator.cs` (the output this tokenizer must be able to read back).
- `src/Yaapic.Core/Import/Curl/CurlTokenizer.cs`, `public static class CurlTokenizer`, `IReadOnlyList<IReadOnlyList<string>> Tokenize(string text)`: returns one word list per command; throws `FormatException(Strings.ImportCurlUnterminatedQuote)` for an unterminated quote.
- Rules (bash subset, final):
  - Normalize `\r\n` to `\n`. A backslash immediately followed by a newline (outside single quotes) is a line continuation and produces nothing; the same for `^` followed by a newline outside quotes (Windows cmd continuation).
  - Words are separated by spaces and tabs. An unquoted newline ends the command (the command is kept when it has at least one word). Unquoted `;` and `&&` also end a command **only** when the next word is `curl` (otherwise they are ordinary characters).
  - `#` starting a word (preceded by whitespace or at the start) begins a comment to the end of the line. A leading `$ ` or `> ` prompt on a line is dropped.
  - Single quotes: everything literal up to the next `'`. Double quotes: `\` escapes only `"`, `\`, `$`, `` ` `` and newline (a `\` before another character stays as a backslash and that character); `$VAR` and `$(...)` are **not** expanded (kept as text).
  - ANSI-C quotes `$'...'`: escapes `\n \r \t \\ \' \" \a \b \f \v \e \0`, `\xHH`, `\uHHHH`, `\UHHHHHHHH`, `\NNN` octal (1 to 3 digits); an unknown escape keeps the backslash and the character. Used by the browsers' "Copy as cURL (bash)" for `--data-raw`.
  - Outside quotes a backslash escapes the next character (`\ ` is a space in the word).
  - Adjacent quoted and unquoted pieces concatenate into one word (`-H'a: b'`, `"a"'b'` -> `ab`).
  - Windows cmd form (`^"` quoting): when a command contains the sequence `^"` the tokenizer throws `FormatException(Strings.ImportCurlCmdSyntax)` (the message tells the user to copy as bash).
  - Only commands whose first word is `curl` or `curl.exe` are returned; other lines before or between are ignored. No command at all: an empty list.
- Strings (EN and FR): `import.curl.unterminatedQuote` = "The command has an unterminated quote."; `import.curl.cmdSyntax` = "This looks like the Windows cmd form of cURL. Copy the command as \"cURL (bash)\" and paste it again."

## Out of scope

Option meaning (M5-18), variable expansion, PowerShell syntax.

## Acceptance criteria

- [x] Tests with exact word lists: simple command; single and double quotes with an embedded other quote; `\"` inside double quotes; backslash-newline continuation (several lines); `$'...'` with `\n`, `\'`, `é`, `\x41`, octal; adjacent pieces; a `#` comment line and an inline comment; two commands separated by newline and by `;` before `curl`;
  `a;b` inside a word stays; a prompt `$ curl`; `curl.exe`; a text without curl returns empty; an unterminated quote throws; a `^"` command throws; the output of `CurlCodeGenerator` for `CodeSamples` entries (M4-50) tokenizes back to the expected method, URL, header and body words (use two or three samples).
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the cURL command tokenizer`
