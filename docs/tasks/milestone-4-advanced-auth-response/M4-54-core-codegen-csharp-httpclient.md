# M4-54: Core: code generator, C# (HttpClient)

Status: todo
Platform: any
Depends on: M4-50

## Goal

`CSharpHttpClientCodeGenerator` produces a runnable top-level-statements C# snippet using `HttpClient`.

## Design decisions

- Read first: ADR 0015, M4-50. `Id` `csharp-httpclient`, `DisplayName` `C# (HttpClient)`, `EditorLanguage` `csharp`; 4-space indent.
- Shape:
  ```csharp
  using var client = new HttpClient();
  using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.example.com/users?id=1");
  request.Headers.TryAddWithoutValidation("Accept", "application/json");
  request.Content = new StringContent("{\"a\":1}", Encoding.UTF8, "application/json");

  using var response = await client.SendAsync(request);
  Console.WriteLine((int)response.StatusCode);
  Console.WriteLine(await response.Content.ReadAsStringAsync());
  ```
  Needs `using System.Text;` only when a `StringContent` with `Encoding.UTF8` is emitted (the `using` line is first in the file).
  - Methods: `HttpMethod.Get/Post/Put/Delete/Head/Options/Patch`; any other method name as `new HttpMethod("NAME")`.
  - Headers: `Content-*` headers (`Content-Type` is handled by the content constructor, others through `request.Content.Headers.TryAddWithoutValidation`
    after the content is created) versus request headers through `request.Headers.TryAddWithoutValidation`; a `Content-Type` header with a body is passed to the
    `StringContent` constructor as its third argument (media type only: the text before `;`; the charset is dropped because `Encoding.UTF8` is passed).
  - Body: `Text` as above; `Form` as `new FormUrlEncodedContent(new Dictionary<string, string> { ["a"] = "1" })` (a repeated name makes a `List<KeyValuePair<string,string>>`
    instead: use that form whenever any name repeats); `Multipart` as `new MultipartFormDataContent { { new StringContent("value"), "name" }, { new
    StreamContent(File.OpenRead("/tmp/report.pdf")), "file", "report.pdf" } }`; `File` as `new StreamContent(File.OpenRead("/path"))` with the content type header set
    on `request.Content.Headers.ContentType` when known.
  - String literals: regular `"..."` with escapes `\\`, `\"`, `\n`, `\r`, `\t`, `\0`, other control characters as `\uXXXX`.
- `AuthNote` as a `//` comment on the first line. Trailing newline.

## Out of scope

`RestSharp`, `Refit`, a `Main` method and namespace boilerplate, `IHttpClientFactory`.

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests`: exact expected text for every sample of `CodeSamples`; escape helper on each special character; repeated form field; content header
  placement; `using System.Text;` only when needed; unknown method.
- [ ] No compile test (it would need a new package): the output of each sample is pasted into a scratch console project outside the repository and built once
  by hand with the SDK in use; record the result in the card's Notes.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the C# HttpClient code generator`
