# M4-23: Core: authorization-code flow ports and orchestration

Status: todo
Platform: any
Depends on: M4-21, M4-22

## Goal

`IOAuth2TokenService.GetNewTokenAsync` runs the authorization-code flow through two small ports, testable without a browser or a socket.

## Design decisions

- Read first: ADR 0013 (Authorization-code redirect), M4-21, M4-22.
- Ports in `Yaapic.Core.Auth.OAuth2`:
  - `public interface ILoopbackRedirectReceiver { Task<string> WaitForRequestTargetAsync(int port, string path, string successHtml,
    CancellationToken ct); }` returns the request target (`/callback?code=...`) of the first request to `path`, answers the browser with
    `successHtml`, then stops listening; throws `OperationCanceledException` on cancel and `InvalidOperationException` when the port is
    busy (message user-presentable). Implemented by M4-24.
  - `public interface IExternalBrowser { Task OpenAsync(Uri url, CancellationToken ct); }` opens the system browser (M4-25).
- `OAuth2TokenService` (M4-21) gets two more constructor parameters, `ILoopbackRedirectReceiver receiver` and `IExternalBrowser browser`
  (the parameters before are unchanged; update M4-21's tests and composition accordingly). `GetNewTokenAsync` for `AuthorizationCode`:
  1. Validate `AuthorizationUrl`, `TokenUrl`, `ClientId`; `TryGetListenAddress(RedirectUri)` else `Fail(Strings.OAuth2RedirectUriInvalid)`.
  2. `Pkce.Create()`, `NewState()`, build the URL.
  3. Start waiting on the receiver **before** opening the browser (so a fast redirect is not lost), open the browser, await the target with a
     3-minute timeout (`CancellationTokenSource.CancelAfter`; a timeout gives `Fail(Strings.OAuth2AuthorizationTimeout)`).
  4. `ParseCallback`: an `error` gives `Fail("error: description")`; a `state` mismatch or missing gives `Fail(Strings.OAuth2StateMismatch)`;
     a missing `code` gives `Fail(Strings.OAuth2InvalidResponse(0))`.
  5. Exchange the code with `OAuth2TokenRequests.AuthorizationCode` through the transport, parse, cache, return.
- `successHtml` is a constant in Core, `Strings.OAuth2CallbackPage`-independent plain English HTML ("You can close this tab and return to
  Yaapic."), no external resources, no script.
- Strings `OAuth2AuthorizationTimeout`, `OAuth2StateMismatch`, EN and FR.
- Test doubles `FakeLoopbackRedirectReceiver` (returns a configured target or throws) and `FakeExternalBrowser` (records the URL) in
  `tests/Yaapic.TestDoubles/`.

## Out of scope

The real listener and browser launcher (M4-24, M4-25), the authenticator (M4-26), UI buttons.

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests`: happy path (the opened URL has the expected parameters, the exchange request carries the verifier that
  matches the challenge, the token is cached and returned); error redirect; state mismatch; missing state; missing code; timeout (fake time
  or a short injected timeout); cancel; invalid redirect URI; busy port message surfaces; the browser is opened only after the receiver
  started waiting (order recorded by the doubles).
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): run the OAuth 2.0 authorization-code flow behind two ports`
