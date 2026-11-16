# M4-52: Core: code generator, JavaScript (fetch)

Status: done
Platform: any
Depends on: M4-50

## Goal

`JavaScriptFetchCodeGenerator` produces a runnable `fetch` snippet (browser and Node 18+).

## Design decisions

- Read first: ADR 0015, M4-50. `Id` `javascript-fetch`, `DisplayName` `JavaScript (fetch)`, `EditorLanguage` `javascript`; 2-space indent.
- Shape:
  ```js
  const response = await fetch("https://api.example.com/users?id=1", {
    method: "POST",
    headers: {
      "Content-Type": "application/json"
    },
    body: "{\"a\":1}"
  });
  const data = await response.text();
  console.log(response.status, data);
  ```
  - `headers` is an object literal with **quoted** keys, omitted when there are none; duplicate header names are merged into one entry joined with `, `.
  - `body`: `Text` as a JSON string literal; `Form` as `new URLSearchParams({ "a": "1" })` (the browser sets the content type: drop an explicit
    `Content-Type` that equals `application/x-www-form-urlencoded`); `Multipart` as a `FormData` built before the call (`const form = new FormData();
    form.append("name", "value");` and for a file part `form.append("file", await openAsBlob("/path"))` with the first line `import { openAsBlob } from
    "node:fs";`; drop the explicit `Content-Type`); `File` as `body: await openAsBlob("/path")` with the same import (Node 20 API, noted in a comment
    `// Node.js 20+`); `None` has no `body`.
  - `GET` omits `method`.
- String escaping helper `Escape`: `\\`, `\"`, `\n`, `\r`, `\t`, other control characters as `\uXXXX`, and `U+2028`/`U+2029` as ` `/` `; non-ASCII is written as is.
- `AuthNote` as a `//` comment on the first line. Trailing newline.

## Out of scope

`axios`, XHR, a Node `http` version, top-level-await wrapping.

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests`: exact expected text for every sample of `CodeSamples`; the escape helper on each special character; header merging;
  `Content-Type` dropped for form and multipart.
- [x] A test, skipped when `node` is not on the PATH, that runs `node --check` on the generated text of each sample (wrapped in an async IIFE file) and
  expects exit code 0.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the JavaScript fetch code generator`
