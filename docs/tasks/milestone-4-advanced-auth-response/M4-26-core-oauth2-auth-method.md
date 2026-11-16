# M4-26: Core: OAuth 2.0 authentication method

Status: done
Platform: any
Depends on: M4-10, M4-21

## Goal

A request with OAuth 2.0 authentication is sent with the access token in its header or query string.

## Design decisions

- Read first: ADR 0013 (OAuth 2.0 grants, Token lifetime, When tokens are fetched), M4-10, M4-21.
- `public sealed class OAuth2AuthMethod(IOAuth2TokenService tokens) : IAuthMethod`, `Kind` = `OAuth2`, in `Yaapic.Core.Auth.OAuth2`.
- `ApplyAsync` with `auth.OAuth2` (null = `Default`): `var result = await tokens.GetTokenAsync(settings, ct)`; an error returns
  `AuthenticationResult.Fail(error)`; with a token: placement `Header` adds `Authorization: <prefix> <token>` through
  `WithHeaderUnlessPresent` (prefix = `HeaderPrefix`, or `TokenType` from the response when the prefix is empty, or `Bearer`; an empty
  prefix **and** empty token type gives the bare token), placement `Query` adds `WithQueryParameter(QueryParameterName or "access_token",
  token)`.
- A user `Authorization` header wins (then no token is requested: check the header **before** calling the service, so no needless network
  call happens).
- The message of a failed token fetch is passed through unchanged (it never contains tokens or secrets by construction of M4-20), but it
  is prefixed with `Strings.OAuth2TokenError` ("OAuth 2.0: ").
- String `OAuth2TokenError`, EN and FR.

## Out of scope

The composite (M4-27), UI, the interactive button.

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests` with `FakeOAuth2TokenService`: header with default, custom and empty prefix; token type fallback; query
  placement and custom parameter; error propagation with prefix; the service is not called when the user set `Authorization`; the
  settings passed to the service are the resolved ones.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add the OAuth 2.0 authentication method`
