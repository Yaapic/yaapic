# M4-57: Core: code generator, Go (net/http)

Status: todo
Platform: any
Depends on: M4-50

## Goal

`GoCodeGenerator` produces a runnable `main` program using `net/http`.

## Design decisions

- Read first: ADR 0015, M4-50. `Id` `go-nethttp`, `DisplayName` `Go (net/http)`, `EditorLanguage` `go`; tab indentation (as `gofmt`).
- Shape:
  ```go
  package main

  import (
  	"fmt"
  	"io"
  	"net/http"
  	"strings"
  )

  func main() {
  	body := strings.NewReader("{\"a\":1}")
  	req, err := http.NewRequest("POST", "https://api.example.com/users?id=1", body)
  	if err != nil {
  		panic(err)
  	}
  	req.Header.Add("Content-Type", "application/json")

  	res, err := http.DefaultClient.Do(req)
  	if err != nil {
  		panic(err)
  	}
  	defer res.Body.Close()

  	data, err := io.ReadAll(res.Body)
  	if err != nil {
  		panic(err)
  	}
  	fmt.Println(res.StatusCode, string(data))
  }
  ```
  - Imports are exactly those used, sorted as `gofmt` does (no blank imports); `strings` only with a text or form body, `bytes`, `mime/multipart`,
    `os` and `io` as needed by multipart and file bodies. A request without a body passes `nil`.
  - `Form`: `url.Values` (`import "net/url"`), `strings.NewReader(form.Encode())` and a `Content-Type` header of
    `application/x-www-form-urlencoded` (kept explicitly).
  - `Multipart`: a `bytes.Buffer` and `multipart.NewWriter`, `WriteField` for text parts, `CreateFormFile` plus `os.Open` and `io.Copy` for file parts, `writer.Close()`,
    and `req.Header.Set("Content-Type", writer.FormDataContentType())` after the request is created.
  - `File`: `os.Open(path)` body with `defer f.Close()`.
  - `req.Header.Add` per header (so duplicate names stay separate).
- Go string literals: interpreted strings with escapes `\\`, `\"`, `\n`, `\r`, `\t`, other control characters as `\xNN`; non-ASCII as is.
- `AuthNote` as a `//` comment before `package main`. Trailing newline. The generated text must be `gofmt`-stable (tabs, blank lines as shown).

## Out of scope

Custom clients and timeouts, `fasthttp`, redirects.

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests`: exact expected text for every sample of `CodeSamples` (imports differ per sample); escape helper; import selection and order;
  header duplicates.
- [ ] A test, skipped when `gofmt` is not on the PATH, that runs `gofmt -e` on the generated text and expects output equal to the input (already formatted).
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the Go code generator`
