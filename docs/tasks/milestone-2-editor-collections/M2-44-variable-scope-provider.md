# M2-44: Variable scope provider: sending with all scopes

Status: todo
Platform: any
Depends on: M2-35, M2-41, M2-42, M2-43

## Goal

Every send resolves variables from the active environment, the request's collection and the globals.

## Design decisions

- `Yaapic.ViewModels.Requests`: `interface IVariableScopeProvider { VariableScopes GetScopes(Guid requestId);
  event EventHandler? ScopesChanged; }`, and `sealed class VariableScopeProvider(IActiveEnvironmentProvider environments,
  GlobalsViewModel globals, CollectionTreeViewModel collections) : IVariableScopeProvider`: environment =
  `environments.Current`, collection = `collections.FindCollectionVariables(requestId)`, globals = `globals.Current`.
  `ScopesChanged` is raised when the active environment changes (it takes the `EnvironmentsViewModel` as
  `IActiveEnvironmentProvider` **and** needs its `ActiveEnvironmentChanged` event, so change the constructor parameter to
  `EnvironmentsViewModel environments`), when `globals.Variables.Changed` fires, and after the collection tree refreshes
  (add an event `Refreshed` to `CollectionTreeViewModel`).
- `RequestEditorViewModel.FromDefinition/CreateNew` take `IVariableScopeProvider scopes` instead of
  `IActiveEnvironmentProvider environments`; `SendAsync` calls `_sender.SendAsync(ToDefinition(), scopes.GetScopes(Id),
  ct)`. Update `TabsViewModel` factory callers, `AppComposition` and every test; add `FakeVariableScopeProvider` to
  `tests/Yaapic.TestDoubles` (settable `Scopes`, `RaiseScopesChanged()`).
- `AppComposition.CreateShell` creates `FileGlobalsStore`, `GlobalsViewModel` and the provider (the shell does not expose
  the globals yet: M2-45).
- `IActiveEnvironmentProvider` stays (still implemented by `EnvironmentsViewModel`).

## Out of scope

Exposing `Globals` on the shell and loading it (M2-45), hints (M2-46).

## Acceptance criteria

- [ ] Tests in `Yaapic.ViewModels.Tests`: the provider combines the three scopes for a request inside a collection
      (collection variables of its top-level folder) and for an unfiled request (none); `ScopesChanged` on environment
      change, globals edit and tree refresh; sending a request uses a collection variable and a globals variable (assert on
      `FakeHttpTransport`); precedence environment over collection over globals through the whole path; every M1 send
      test still green.
- [ ] 0 warnings; architecture tests green.

## Suggested commit message

`feat(viewmodels): send requests with environment, collection and globals variables`
