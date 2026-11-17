# M5-39: Core: Postman export, authentication and collection variables

Status: todo
Platform: any
Depends on: M5-38

## Goal

Request authentication and collection variables are written in the Postman collection.

## Design decisions

- Read first: M5-38, M5-14 (the table: this card is its inverse), `src/Yaapic.Domain/Http/RequestAuth.cs`.
- `src/Yaapic.Core/Export/Postman/PostmanAuthWriter.cs` (internal static): `DataNode? Write(RequestAuth auth)` returns null for `None`. Shape: `{ "type": "<t>", "<t>": [ { "key", "value", "type": "string" }, ... ] }` (parameters in the order below; every parameter value is written as a string with `"type": "string"`, booleans as `"true"`/`"false"`).

  | Yaapic | `type` and parameters (in order) |
  |---|---|
  | Basic | `basic`: username, password |
  | Bearer | `bearer`: token |
  | ApiKey | `apikey`: key, value, in (`header` or `query`) |
  | Digest | `digest`: username, password |
  | AwsV4 | `awsv4`: accessKey, secretKey, sessionToken, region, service |
  | OAuth2 | `oauth2`: grant_type (`client_credentials`, `authorization_code`, `password_credentials`), accessTokenUrl, authUrl, clientId, clientSecret, scope, audience, username, password, redirect_uri, addTokenTo (`header` or `queryParams`), headerPrefix, client_authentication (`header` or `body`) |
  | Jwt | `jwt`: algorithm, secret, isSecretBase64Encoded (`true`/`false`), privateKey, payload, header (ExtraHeaders), addTokenTo (`header` or `queryParams`), headerPrefix, queryParamKey |

  Empty-string parameters are written (importers read missing keys as empty anyway). The request item gets `"auth": <node>` after `url` in the `request` object when not null.
- **Variables**: the collection folder's `Variables` -> top-level `"variable": [ { "key", "value", "type": "string" } ]` plus `"disabled": true` for disabled rows and `"type": "secret"` for secret rows (value already blank, M5-36); omitted when empty. Nested folder variables are not exported (Yaapic does not use them).
- No folder or collection `auth` is written (Yaapic has none).
- Every Yaapic auth kind has a Postman equivalent, so this card adds no report entries.

## Out of scope

Scripts (M5-40).

## Acceptance criteria

- [ ] Tests with exact expected JSON per kind (each row of the table, including OAuth 2 grants and placements, JWT base64 flag); `None` writes no `auth`; collection variables incl. disabled and secret rows; empty variables omit the member.
- [ ] A round-trip test with `PostmanCollectionImporter` + M5-14 for every auth kind (export, import, equal `RequestAuth`); if M5-14 is not `done`, leave it to M5-66 and say so in the Notes.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): export Postman authentication and variables`
