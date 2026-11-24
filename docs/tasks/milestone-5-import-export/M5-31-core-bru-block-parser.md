# M5-31: Core: Bruno `.bru` block parser

Status: done
Platform: any
Depends on: M5-04

## Goal

A tolerant parser for the Bru markup, producing blocks of key/value pairs, text, or lists, so the Bruno cards only map blocks to Yaapic.

## Design decisions

- Read first: ADR 0017 (Bruno). The Bru format (what the parser must read):

  ```
  meta {
    name: Create user
    type: http
    seq: 2
  }

  post {
    url: {{baseUrl}}/users?x=1
    body: json
    auth: bearer
  }

  params:query {
    page: 1
    ~limit: 10
  }

  headers {
    Content-Type: application/json
    ~X-Off: x
  }

  auth:bearer {
    token: {{token}}
  }

  body:json {
    {
      "name": "a"
    }
  }

  vars:secret [
    token,
    apiKey
  ]

  script:pre-request {
    bru.setVar("a", 1);
  }
  ```

- `src/Yaapic.Core/Import/Bruno/BruFile.cs` and `BruParser.cs` (namespace `Yaapic.Core.Import.Bruno`, all types `internal`; `Yaapic.Core.Tests` sees them through `InternalsVisibleTo`):
  - `internal sealed record BruBlock(string Name, BruBlockKind Kind, IReadOnlyList<BruPair> Pairs, string Text, IReadOnlyList<string> Items)`; `enum BruBlockKind { Dictionary, Text, List }`; `internal sealed record BruPair(string Key, string Value, bool Enabled)`.
  - `internal sealed class BruFile(IReadOnlyList<BruBlock> blocks)`: `BruBlock? Get(string name)` (first block with that name, ordinal), `IEnumerable<BruBlock> GetAll(string prefix)`.
  - `internal static class BruParser { public static BruFile Parse(string text); }`.
- **Rules** (final): normalize `\r\n` to `\n`. A block starts at a line `name {` or `name [` at column 0 where `name` is `[A-Za-z0-9:_-]+` (spaces before the bracket allowed). A `{` block ends at the first line that is exactly `}` at column 0; a `[` block at the first line that is exactly `]`.
  Lines outside blocks are ignored.
  - **Kind by name**: blocks named `body:json`, `body:text`, `body:xml`, `body:sparql`, `body:graphql`, `body:graphql:vars`, `script:pre-request`, `script:post-response`, `tests`, `docs` are `Text` blocks (`assert` is a dictionary): `Text` is the inner lines with **two leading spaces removed** (lines with fewer leading spaces keep them), joined with `\n`, without a trailing newline.
    `[` blocks (`vars:secret`) are `List`: each inner line split on `,`, trimmed, empty dropped. Every other `{` block is a `Dictionary`.
  - **Dictionary lines**: trimmed; `key: value` split at the first `:`; both trimmed (value may contain further colons, e.g. URLs); a key starting with `~` is disabled (`Enabled = false`, the `~` removed); an empty line or a line without `:` is ignored.
    A value that is exactly `'''` begins a multi-line value that runs to a line (trimmed) equal to `'''`; the lines in between (with the block's two-space indent removed) joined by `\n` are the value (used by long header and body values).
  - Never throws on malformed content: an unterminated block (no closing line) ends at the end of the file; text before the first block is ignored.
- Strings: none.

## Out of scope

Mapping blocks (M5-32 onward), writing Bru (M5-45).

## Acceptance criteria

- [x] Tests with the sample above and more: meta, verb block, params with a disabled entry, headers with a URL value containing colons, auth block, `body:json` with nested braces and indentation (text without the two-space indent; deeper indentation kept), `vars:secret` list, script block, `'''` multi-line value, CRLF input, an unterminated block,
  a `body:text` containing a line `}` indented (not a terminator because it is not at column 0), text before the first block ignored, `Get` and `GetAll("auth:")`, an empty file.
- [x] Architecture tests green, 0 warnings, XML docs where public, all tests green.

## Suggested commit message

`feat(core): add the Bruno .bru block parser`
