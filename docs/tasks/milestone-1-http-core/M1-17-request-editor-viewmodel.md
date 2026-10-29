# M1-17: Request editor ViewModel

Status: todo
Platform: any
Depends on: M1-16

## Goal

The ViewModel behind one request tab: edit fields, convert to and from the domain, track unsaved changes.

## Design decisions

- `partial class RequestEditorViewModel : ObservableObject` in `Yaapic.ViewModels.Requests`.
- State: `Guid Id` (read-only), observable `Name`, `Method` (`HttpMethodKind`), `Url`, `BodyKind`, `BodyContentType`
  (`RawContentType`), `BodyText`; `KeyValueEditorViewModel QueryParams` and `Headers`.
- Static option lists for the views: `IReadOnlyList<HttpMethodKind> Methods = [Get, Post]`,
  `BodyKinds = [None, Raw]`, `ContentTypes = [Json, Text, Xml]`.
- `static RequestEditorViewModel FromDefinition(HttpRequestDefinition definition, bool isNew = false)` and
  `HttpRequestDefinition ToDefinition()` (name trimmed; blank name -> `"New Request"`).
- `bool IsNew` (never saved), `bool IsDirty`: `IsNew || !ToDefinition().IsEquivalentTo(baseline)`, recomputed on every
  field or editor `Changed` and raised as a property change. `void MarkSaved()` sets the baseline to `ToDefinition()`,
  `IsNew = false`.
- `string Title` = `Name` (or `"New Request"` when blank), plus `" •"` when `IsDirty`; raised when either changes.
- `static RequestEditorViewModel CreateNew()` = `FromDefinition(HttpRequestDefinition.Create("New Request"), isNew: true)`.

## Out of scope

Send and response (M1-19), saving to a store (M1-20), URL/query-param two-way sync (M2), validation messages.

## Acceptance criteria

- [ ] Tests in `Yaapic.ViewModels.Tests`: `FromDefinition` then `ToDefinition` is equivalent; a loaded request is not
      dirty; editing name, URL, method, a header, a param, body kind or body text makes it dirty and changes `Title`;
      reverting the edit makes it clean again; `MarkSaved` clears dirty; `CreateNew` is dirty and `IsNew`; blank name
      becomes "New Request" in `ToDefinition`.
- [ ] 0 warnings.

## Suggested commit message

`feat(viewmodels): add request editor view model`
