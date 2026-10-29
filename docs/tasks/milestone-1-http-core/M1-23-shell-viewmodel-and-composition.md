# M1-23: Shell ViewModel and composition root

Status: todo
Platform: any
Depends on: M1-14, M1-15, M1-21, M1-22

## Goal

One `ShellViewModel` that both App projects bind to, built by a shared composition project, with session restore.

## Design decisions

- `partial class ShellViewModel : ObservableObject` in `Yaapic.ViewModels.Shell`, constructor
  `(TabsViewModel tabs, RequestListViewModel requests, EnvironmentsViewModel environments, ISessionStore session)`,
  exposing the three as properties.
- `Task InitializeAsync()`: `session.LoadAsync`; `environments.LoadAsync(state.ActiveEnvironmentId)`;
  `requests.RefreshAsync()`; open, in order, every `OpenRequestIds` entry that exists in the store (others dropped);
  activate `ActiveRequestId` if open, else the first tab; if no tab is open, call `tabs.NewTab()`. No session save while
  initializing.
- After initialization, `tabs.SessionChanged` and `environments.ActiveEnvironmentChanged` save a `SessionState` built
  from the saved tabs only (new, never-saved tabs are excluded) and the active environment id. Saves are serialized
  (one at a time, latest state wins); a failing save is ignored (session state is not user data).
- `Task<bool> RequestCloseAsync()` (called by the window before it closes; tabs are **not** closed so the session keeps
  them): for each dirty tab in tab order, set it active and ask `dialogs.ConfirmUnsavedAsync(tab.Name)`: `Save` ->
  `tabs.SaveAsync(tab)`, return false if it fails; `Discard` -> continue; `Cancel` -> return false (the window stays
  open). Untouched new tabs are skipped. Then save the session and return true. The constructor therefore also takes
  `IDialogService dialogs`.
- New project `src/Yaapic.Composition` (`net11.0`), references `Yaapic.ViewModels`, `Yaapic.Infrastructure`,
  `Yaapic.Core`, `Yaapic.Domain`, `Yaapic.UI.Abstractions`. One public static method:
  `ShellViewModel AppComposition.CreateShell(string workspaceRoot, IDialogService dialogs)`: `Workspace.EnsureCreated`,
  file stores, `HttpClientTransport(DefaultHttpClient.Create(), TimeSpan.FromSeconds(30))`, `RequestSender`,
  `EnvironmentsViewModel`, `TabsViewModel` with a factory that passes the sender and the environments view model,
  `RequestListViewModel`, `ShellViewModel`. No DI container.
- `DependencyRulesTests`: add `["Yaapic.Composition"] = ["Yaapic.Core", "Yaapic.Domain", "Yaapic.Infrastructure",
  "Yaapic.UI.Abstractions", "Yaapic.ViewModels"]` and change `Yaapic.App.WinUI` to
  `["Yaapic.Composition", "Yaapic.UI.Abstractions", "Yaapic.ViewModels"]`. Add a rule test: `Yaapic.ViewModels` must not
  reference `Yaapic.Infrastructure` (already implied; keep the explicit assertion).

## Out of scope

Views, DI container, logging, settings, workspace picker.

## Acceptance criteria

- [ ] `ShellViewModel` tests with in-memory doubles: restore order, missing ids dropped, active tab restored, empty
      session opens one new tab, active environment restored; session saved on tab open/close/switch and on environment
      change, never-saved tabs excluded; `RequestCloseAsync` with Cancel returns false.
- [ ] A test in `Yaapic.Infrastructure.Tests` (add a `Yaapic.Composition` reference) calls `CreateShell` on a temp
      directory with a `FakeDialogService`, then `InitializeAsync`, and finds one new tab.
- [ ] 0 warnings, architecture tests green with the new rules.

## Suggested commit message

`feat: add shell view model and composition root`
