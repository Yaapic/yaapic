# M2-06: Request resolution: authentication

Status: done
Platform: any
Depends on: M2-05

## Goal

Basic, Bearer and API key authentication are applied by `RequestResolver`.

## Design decisions

- After variable substitution of `Username`, `Password`, `Token`, `Key` and `Value` (unresolved names go to
  `Unresolved`):
  - Basic: header `Authorization: Basic <base64(UTF-8 "user:pass")>`; skipped when username and password are both empty.
  - Bearer: `Authorization: Bearer <token>`; skipped when the token is empty after trimming.
  - ApiKey, `Header`: header `<key>: <value>`; `Query`: parameter appended to the query string after the request's own
    parameters, escaped like M1 (`Uri.EscapeDataString`); skipped when the key is empty.
- When the user already has an **enabled** header with the same name (ordinal, ignoring case), that header wins and the
  auth is not added (for Basic and Bearer the name is `Authorization`).
- Auth headers are appended after the user's headers.

## Out of scope

OAuth 2.0, JWT, digest, AWS signing (M4), the UI (M2-18, M2-23).

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests`: each auth kind with its expected header or query (Basic for `Aladdin` /
      `open sesame` is `Basic QWxhZGRpbjpvcGVuIHNlc2FtZQ==`); variables in fields; unresolved reported; skipped when
      empty; a user `Authorization` header wins; API key in the query goes after the params and before the fragment;
      `None` changes nothing.
- [x] 0 warnings.

## Suggested commit message

`feat(core): apply basic, bearer and api key authentication`
