# M4-10: Core: authenticator port and send pipeline stage

Status: done
Platform: any
Depends on: M4-09

## Goal

`RequestSender` applies a pending authentication through a port before calling the transport, and a failed authentication is a normal
send failure.

## Design decisions

- Read first: ADR 0013, `RequestSender.cs`, `RequestResolver.cs` (`AppendQuery` is private today), `SendFailureKind.cs`, M3-11 (how the
  scripted sender wraps the plain sender), `tests/Yaapic.TestDoubles/FakeHttpTransport.cs`.
- Namespace `Yaapic.Core.Auth`, folder `src/Yaapic.Core/Auth/`:
  - `public interface IAuthMethod { AuthKind Kind { get; } Task<AuthenticationResult> ApplyAsync(ResolvedRequest request, ResolvedAuth auth,
    CancellationToken ct); }` (one per kind, M4-13, M4-16, M4-18, M4-26).
  - `public sealed record AuthenticationResult(ResolvedRequest? Request, string? ErrorMessage)` with `Success(ResolvedRequest)` and
    `Fail(string message)`.
  - `public interface IRequestAuthenticator { Task<AuthenticationResult> AuthenticateAsync(ResolvedRequest request, CancellationToken ct); }`
    (returns the request unchanged when `PendingAuth` is null).
  - `public static class ResolvedRequestAuthExtensions`: `WithHeaderUnlessPresent(this ResolvedRequest, name, value)` (ordinal-ignore-case
    check like M2-06, appended after the user's headers) and `WithQueryParameter(this ResolvedRequest, name, value)` (escaped with
    `Uri.EscapeDataString`, appended after existing parameters and before the fragment: make the resolver's `AppendQuery` an `internal static`
    helper in `Yaapic.Core.Http` and reuse it; its existing tests must stay green).
- `SendFailureKind` gains `AuthenticationFailed` (appended).
- `RequestSender` constructor becomes `(IHttpTransport transport, IRequestAuthenticator? authenticator = null)`. After resolving: when
  `resolved.PendingAuth` is not null, call the authenticator; no authenticator or a `Fail` result gives
  `SendResult.Fail(SendFailureKind.AuthenticationFailed, message)` (no authenticator: `Strings.AuthNotAvailable`); cancellation maps to
  `Canceled` as today; an unexpected exception maps to `AuthenticationFailed` with its message. The transport is **not** called after a
  failure. `UnresolvedVariables` still returned.
- Localization: `Strings.AuthNotAvailable` ("This authentication type is not available.") in `strings.en.json` and `strings.fr.json`,
  regenerate `Strings.g.cs` as the Localization tests describe.
- Test double `FakeRequestAuthenticator` in `tests/Yaapic.TestDoubles/` (records requests, returns a configured result or throws).

## Out of scope

Any concrete authenticator, the composite (M4-27), composition wiring (M4-61).

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests`: header added and sent; a user `Authorization` header wins; query parameter placement before the
  fragment and after params; failure does not call the transport and returns `AuthenticationFailed`; no authenticator with a pending
  auth fails cleanly; no pending auth never calls the authenticator; cancellation; `AppendQuery` tests unchanged.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the authenticator port and apply it before sending`
