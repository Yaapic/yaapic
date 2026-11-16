# M4-53: Core: code generator, Python (requests)

Status: todo
Platform: any
Depends on: M4-50

## Goal

`PythonRequestsCodeGenerator` produces a runnable Python 3 script using the `requests` package.

## Design decisions

- Read first: ADR 0015, M4-50. `Id` `python-requests`, `DisplayName` `Python (requests)`, `EditorLanguage` `python`; 4-space indent.
- Shape:
  ```python
  import requests

  url = "https://api.example.com/users?id=1"
  headers = {
      "Accept": "application/json",
  }
  payload = "{\"a\":1}"

  response = requests.request("POST", url, headers=headers, data=payload.encode("utf-8"))
  print(response.status_code, response.text)
  ```
  - `headers` dict omitted when empty; duplicate names merged with `, `.
  - Body: `Text` as `data=payload.encode("utf-8")` with `payload` a string literal; `Form` as `data={...}` dict (a repeated field name becomes a list
    of values), explicit `application/x-www-form-urlencoded` header dropped; `Multipart` as `files=[("name", (None, "value")), ("file",
    ("report.pdf", open("/tmp/report.pdf", "rb")))]` and the explicit `Content-Type` dropped; `File` as `data=open("/path", "rb")`; `None` has no body
    argument.
  - Method written as the literal upper-case string in `requests.request(...)`.
- String literals use double quotes with escapes `\\`, `\"`, `\n`, `\r`, `\t`, other control characters as `\xNN`, non-ASCII as is; a multi-line value stays on one line
  with `\n`.
- `AuthNote` as a `#` comment above the import. One blank line between blocks; trailing newline.

## Out of scope

`http.client`, `httpx`, sessions, error handling.

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests`: exact expected text for every sample of `CodeSamples`; escape helper on each special character; repeated form field;
  header merging; `Content-Type` dropped for form and multipart.
- [ ] A test, skipped when `python` is not on the PATH, that runs `python -c "import ast,sys; ast.parse(sys.stdin.read())"` on each generated text and expects
  exit code 0.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the Python requests code generator`
