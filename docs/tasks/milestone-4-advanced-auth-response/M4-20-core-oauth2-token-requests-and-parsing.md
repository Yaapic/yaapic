# M4-20: Core: OAuth 2.0 token requests and response parsing

Status: done
Platform: any
Depends on: M4-19

## Goal

Pure code builds the token endpoint requests for each grant and parses the endpoint's answer.

## Design decisions

- Read first: ADR 0013 (OAuth 2.0 grants), M4-19, `ResolvedRequest`, `System.Text.Json` source generator usage in `Yaapic.Core` (grep for
  `JsonSerializerContext`; if none, use `JsonDocument` for parsing, no serializer needed).
- `public static class OAuth2TokenRequests` in `Yaapic.Core.Auth.OAuth2`, each returning a `ResolvedRequest` (POST,
  `application/x-www-form-urlencoded`, header `Accept: application/json`, `PendingAuth` null, follow redirects off, timeout 30 s):
  - `ClientCredentials(OAuth2Settings s)`: `grant_type=client_credentials`, `scope`, `audience` when non-empty.
  - `Password(OAuth2Settings s)`: `grant_type=password`, `username`, `password`, `scope`, `audience`.
  - `AuthorizationCode(OAuth2Settings s, string code, string codeVerifier, string redirectUri)`: `grant_type=authorization_code`, `code`,
    `redirect_uri`, `code_verifier`, plus `client_id` always.
  - `Refresh(OAuth2Settings s, string refreshToken)`: `grant_type=refresh_token`, `refresh_token`, `scope` when non-empty.
  - Client authentication: `BasicHeader` adds `Authorization: Basic base64(urlencode(clientId):urlencode(clientSecret))` (RFC 6749 form
    encoding of each part) and **omits** `client_id`/`client_secret` from the body except `client_id` for the code exchange as above; with
    `RequestBody` it adds `client_id` and, when non-empty, `client_secret` to the body. `BasicHeader` with an empty client id and secret
    adds no header.
  - Field values use `Uri.EscapeDataString` like the url-encoded body in `RequestResolver`.
- `public static class OAuth2TokenResponses`: `static OAuth2TokenParseResult Parse(HttpResponseData response, DateTimeOffset now)` where
  `OAuth2TokenParseResult(OAuth2Token? Token, string? Error)`.
  - Success (status 2xx, JSON object with a string `access_token`): `TokenType` default `Bearer`; `expires_in` accepted as number or numeric
    string; `ExpiresAt = now + seconds` (absent: null); `refresh_token` and `scope` optional.
  - Error: status not 2xx or a JSON `error` member: message `error` plus `: error_description` when present (never the response body
    otherwise); non-JSON body: `Strings.OAuth2InvalidResponse(status)`.
  - Invalid or missing `access_token` on a 2xx: `Strings.OAuth2InvalidResponse(status)`.
- Strings `OAuth2InvalidResponse`, EN and FR.

## Out of scope

Sending (M4-21), PKCE (M4-22), caching decisions.

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests`: the body and headers of each request kind and both client-auth styles (Basic value for `a b` / `c:d`
  is form-encoded before base64), scope and audience omitted when empty; parsing of: full response, string `expires_in`, missing
  `token_type`, no `expires_in`, `error` with and without description, 401 with plain text, 200 with HTML, missing `access_token`.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): build OAuth 2.0 token requests and parse token responses`
