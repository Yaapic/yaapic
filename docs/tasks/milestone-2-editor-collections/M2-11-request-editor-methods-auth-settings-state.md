# M2-11: Request editor ViewModel: methods, authentication and settings

Status: todo
Platform: any
Depends on: M2-04

## Goal

`RequestEditorViewModel` edits the seven methods, the authentication and the per-request settings.

## Design decisions

- `Methods` lists all seven methods; `ContentTypes` = `[Json, Text, Xml, Html, JavaScript]`. `MethodText` uses
  `Method.ToText()`. New `bool AllowsBody` (`Method.AllowsBody()`), raised when `Method` changes (the views hide the
  body for false).
- New `AuthEditorViewModel` (`partial`, `ObservableObject`) in `Yaapic.ViewModels.Requests`: observable `Kind`
  (`AuthKind`), `Username`, `Password`, `Token`, `Key`, `Value`, `Location` (`ApiKeyLocation`); static lists `Kinds` and
  `Locations`; `event EventHandler? Changed` raised on every property change; `void Load(RequestAuth)`;
  `RequestAuth ToAuth()`; `bool IsActive` (Kind is not None).
- `RequestEditorViewModel` gains `AuthEditorViewModel Auth` (its `Changed` triggers the dirty/title refresh) and
  observable `bool FollowRedirects` (default true) and `int TimeoutSeconds` (default 0; a negative value is coerced to
  0). `FromDefinition` loads `definition.Auth` and `definition.Settings`; `ToDefinition` writes them;
  `IsDirty` therefore reflects them.
- `Title`, `MarkSaved`, baseline and `IsNew` behaviour are unchanged.

## Out of scope

Body kinds (M2-12), URL sync (M2-13), views (M2-16, M2-18, M2-21, M2-23).

## Acceptance criteria

- [ ] Tests in `Yaapic.ViewModels.Tests`: all seven methods round trip and `AllowsBody` follows the method; auth round
      trips for each kind; editing any auth field, `FollowRedirects` or `TimeoutSeconds` makes the tab dirty and reverting
      cleans it; negative timeout becomes 0; `Load` of an auth does not mark the tab dirty; every M1 editor test still
      green.
- [ ] 0 warnings.

## Suggested commit message

`feat(viewmodels): edit all methods, authentication and settings in the request editor`
