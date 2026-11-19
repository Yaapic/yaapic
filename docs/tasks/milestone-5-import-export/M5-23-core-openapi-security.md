# M5-23: Core: OpenAPI import, security schemes to authentication

Status: done
Platform: any
Depends on: M5-20

## Goal

Operation `security` requirements become `RequestAuth` with placeholder variables, so the imported collection is ready to be filled in.

## Design decisions

- Read first: M5-20, M5-14 (the same target types and their defaults), `src/Yaapic.Domain/Http/RequestAuth.cs`, `OAuth2Settings.cs`, ADR 0013.
- `src/Yaapic.Core/Import/OpenApi/OpenApiSecurity.cs` (internal static): `RequestAuth Map(DataNode operation, DataNode root, OpenApiRefResolver refs, string path, ImportReport report, ICollection<string> variableNames)`; called per operation.
- **Selecting the scheme**: the operation's `security` array (when present, even empty: an empty array or `[{}]` means no auth) else the document's top-level `security`; use the **first requirement object** and in it the **first scheme name**; its definition is `components.securitySchemes[name]` (resolved).
  Other requirements and schemes of the same requirement are ignored with one `Info` per import `Strings.ImportOpenApiOtherSecurity`.
- **Placeholder names**: `id = name` with every character that is not a letter, digit or `_` replaced by `_`. Variables created (added once to the collection variables with an empty value, through `variableNames` so the caller dedups): see the table.

  | Scheme | Yaapic |
  |---|---|
  | `type: http`, `scheme: basic` (any case) | `Basic`, username `{{<id>_username}}`, password `{{<id>_password}}` |
  | `type: http`, `scheme: bearer` | `Bearer`, token `{{<id>_token}}` |
  | `type: http`, other scheme (`digest`) | `digest` -> `Digest` with `{{<id>_username}}`, `{{<id>_password}}`; others: `Warning` unsupported, no auth |
  | `type: apiKey` | `ApiKey`, `Key = scheme.name`, `Value = {{<id>_key}}`, `in: query` -> Query, `in: header` -> Header, `in: cookie` -> `Warning` unsupported (cookies), no auth |
  | `type: oauth2` | the first flow present in the order `clientCredentials`, `authorizationCode`, `password`: `OAuth2Settings` with `Grant` accordingly, `TokenUrl = tokenUrl`, `AuthorizationUrl = authorizationUrl` (authorizationCode), `ClientId = {{<id>_client_id}}`, `ClientSecret = {{<id>_client_secret}}`, `Scope` = the scopes listed in the requirement joined by a space (the requirement array values; none: empty), `Username = {{<id>_username}}` and `Password = {{<id>_password}}` for `password`, `RedirectUri = ""`, other fields as `OAuth2Settings.Default`; a document with only `implicit` flows: `Warning` unsupported (implicit), no auth |
  | `type: openIdConnect` | `Warning` unsupported, no auth |
  | unknown or missing definition | `Warning` `Strings.ImportOpenApiSecurityUnknown(path, name)`, no auth |

- Warnings use `Strings.ImportAuthUnsupported(path, type)` (shared with M5-14) with `type` = e.g. `openIdConnect`, `oauth2 implicit`, `apiKey in cookie`.
- Add the shared one-per-import `Info` `Strings.ImportOpenApiSecurityPlaceholders(count)` when placeholder variables were created: "Authentication values were replaced by empty variables (for example {{bearerAuth_token}}); fill them in the collection variables.".
- Strings (EN and FR): `import.openapi.otherSecurity` = "Only the first security requirement of each operation is imported."; `import.openapi.securityUnknown` = "{0}: the security scheme \"{1}\" is not defined."; `import.openapi.securityPlaceholders`.

## Out of scope

Applying scopes per flow, mutual TLS schemes, OpenAPI 3.1 `mutualTLS`.

## Acceptance criteria

- [x] Tests: each row of the table with the exact `RequestAuth` and the variables created (names with a space or dash sanitized, created once even with 10 operations); operation `security: []` overrides the global; global security applies to operations without their own; scopes joined; OAuth flow order;
  implicit-only warns; openIdConnect warns; unknown scheme warns; `apiKey` in cookie warns; the other-security `Info` once; no security at all gives `RequestAuth.None` and no variables.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): import OpenAPI security schemes`
