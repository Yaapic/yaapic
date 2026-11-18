# M4-50: Core: code generation model, registry and shared samples

Status: done
Platform: any
Depends on: M4-04, M4-09

## Goal

The contract every code generator implements, the conversion from a resolved request, the registry, and the sample requests all generator tests share.

## Design decisions

- Read first: ADR 0015 (Code generation contract, What the generated code contains), `ResolvedRequest.cs` (`Method`, `Url`, `Headers`, `BodyText`,
  `BodyContentType`, `Parts`, `BodyFilePath`, `PendingAuth`), `HttpMethodKindExtensions` (method name text). Namespace `Yaapic.Core.CodeGeneration`, folder
  `src/Yaapic.Core/CodeGeneration/`.
- `public abstract record CodeBody` with sealed nested cases: `CodeBody.None`, `Text(string Content, string? ContentType)`,
  `Form(IReadOnlyList<KeyValueEntry> Fields)` (url-encoded: the **decoded** names and values), `Multipart(IReadOnlyList<CodePart> Parts)` with
  `CodePart(string Name, string? Value, string? FilePath)`, `File(string Path, string? ContentType)`.
- `public sealed record CodeRequest(string Method, string Url, IReadOnlyList<KeyValueEntry> Headers, CodeBody Body, string? AuthNote)`.
- `public static class CodeRequestFactory.From(ResolvedRequest request)`:
  - body mapping: `Parts` gives `Multipart`; `BodyFilePath` gives `File`; `BodyText` with `BodyContentType` of `application/x-www-form-urlencoded` gives
    `Form` (split on `&` and `=`, percent-decoded with `Uri.UnescapeDataString` and `+` as space), any other `BodyText` gives `Text`; none gives `None`.
    The `Content-Type` header is **added** to `Headers` from `BodyContentType` when the body exists and no user `Content-Type` header is present
    (except for multipart, where the library sets the boundary: no header).
  - `PendingAuth` kinds: `OAuth2` and `Jwt` add the header `Authorization: Bearer <access token>` (or the configured prefix / query parameter ignored: the
    placeholder is always the header) and set `AuthNote` to null; `Digest` and `AwsV4` leave the headers untouched and set `AuthNote` (English text
    `Digest authentication must be computed by the caller (RFC 7616).` / `AWS Signature v4 must be computed by the caller.`; generators emit it as a comment).
  - `Method` is the upper-case method name.
- `public interface ICodeGenerator { string Id { get; } string DisplayName { get; } string EditorLanguage { get; } string Generate(CodeRequest request); }`
  and `public sealed class CodeGeneratorRegistry(IEnumerable<ICodeGenerator> generators)` with `All` (registration order), `Find(string id)`, duplicate ids
  rejected at construction.
- A small shared helper `CodeText` (internal): `Lines(string)` splitting, `JoinLines(IEnumerable<string>)` with `\n` and a trailing newline, `IsJson(string?
  contentType)`.
- Shared samples in `tests/Yaapic.Core.Tests/CodeGeneration/CodeSamples.cs`: `GetSimple` (GET `https://api.example.com/users?id=1&name=a%20b` with `Accept:
  application/json`), `PostJson` (POST, JSON body containing a double quote, a backslash, a newline and the character `é`), `PostForm` (urlencoded
  `a=1&b=x y`), `PostMultipart` (one text part, one file part `/tmp/report.pdf`), `PutFile` (binary body `/tmp/data.bin`), `HeadersAwkward` (a header value with a
  double quote and a single quote), `Unresolved` (URL with `{{host}}`), `DigestNote`. Each as a `ResolvedRequest` and its `CodeRequest`.

## Out of scope

Any concrete generator, UI, the view model.

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests`: factory body mapping for each case (urlencoded decoding incl. `+` and `%20`), Content-Type header added only when missing and
  never for multipart, the four auth cases, method casing; registry order, lookup, duplicate rejection.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the code generation model and registry`
