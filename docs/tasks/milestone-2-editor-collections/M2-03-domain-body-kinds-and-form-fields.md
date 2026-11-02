# M2-03: Domain: body kinds and form fields

Status: done
Platform: any
Depends on: M2-02

## Goal

`RequestBody` can describe url-encoded forms, multipart forms and a binary file, next to raw text.

## Design decisions

- `BodyKind` gains `UrlEncoded`, `FormData`, `Binary`.
- New `FormFieldKind { Text, File }` and `public sealed record FormField(string Key, string Value, bool Enabled = true,
  FormFieldKind Kind = FormFieldKind.Text)` in `Yaapic.Domain.Http` (for `File`, `Value` is a file path).
- `RequestBody` becomes `(BodyKind Kind, RawContentType ContentType, string Text, IReadOnlyList<FormField> Fields,
  string FilePath)`. Keep a secondary constructor `(BodyKind, RawContentType, string)` that passes `[]` and `""`, so
  existing callers compile. `None` and `Raw(...)` stay; add `UrlEncoded(IReadOnlyList<FormField>)`,
  `FormData(IReadOnlyList<FormField>)` and `Binary(string path)`.
- Value equality: declare `Equals(RequestBody?)` and `GetHashCode()` explicitly, comparing `Fields` with
  `SequenceEqual` (a default record compares list references, which would break `HttpRequestDefinition.IsEquivalentTo`
  and dirty tracking).
- `SendFailureKind` gains `FileUnreadable` (a body file could not be read).
- No other project changes.

## Out of scope

Resolution, transport, YAML, ViewModels.

## Acceptance criteria

- [x] Tests in `Yaapic.Domain.Tests`: two bodies with equal but distinct field lists are equal and have the same hash
      code; a differing field, kind, path or text makes them unequal; `IsEquivalentTo` on a request with form fields;
      factory helpers set the right kind; the three-argument constructor yields empty `Fields` and `FilePath`.
- [x] Whole solution still builds with 0 warnings; existing tests green.

## Suggested commit message

`feat(domain): add url-encoded, form-data and binary body kinds`
