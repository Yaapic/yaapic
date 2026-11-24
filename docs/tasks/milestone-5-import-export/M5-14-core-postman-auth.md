# M5-14: Core: Postman import, authentication

Status: done
Platform: any
Depends on: M5-12

## Goal

Postman `auth` blocks (request, folder and collection level) become `RequestAuth`, with inheritance resolved at import time.

## Design decisions

- Read first: M5-12, `src/Yaapic.Domain/Http/RequestAuth.cs`, `OAuth2Settings.cs`, `JwtSettings.cs`, `AwsV4Settings.cs`, `JwtAlgorithm.cs`, `docs/file-format.md` (Authentication), ADR 0013, ADR 0016 ("Unsupported constructs").
- `src/Yaapic.Core/Import/Postman/PostmanAuth.cs` (internal static): `RequestAuth? Map(DataNode? auth, string path, ImportReport report)` returns null when `auth` is absent (inherit); `RequestAuth.None` for `noauth`.
  The parameters of a type live in the array `auth.<type>` of `{key, value, type}`; also accept the v2.0 object form (`auth.bearer` as an object `{token: "x"}`): build a `Dictionary<string,string>` by key (value as string, booleans as `true`/`false`) with a helper that handles both shapes.
- Mapping (Postman key -> Yaapic):

  | `type` | Yaapic |
  |---|---|
  | `noauth` | `RequestAuth.None` |
  | `basic` | `Basic`: `username`, `password` |
  | `bearer` | `Bearer`: `token` |
  | `apikey` | `ApiKey`: `key`, `value`, `in` = `query` -> `ApiKeyLocation.Query` else Header |
  | `digest` | `Digest`: `username`, `password` (the other Digest keys are ignored) |
  | `awsv4` | `AwsV4` with `AwsV4Settings(accessKey, secretKey, sessionToken, region, service)` |
  | `oauth2` | `OAuth2` with `OAuth2Settings`: `grant_type` `client_credentials` -> ClientCredentials, `authorization_code` and `authorization_code_with_pkce` -> AuthorizationCode, `password_credentials` -> Password, other: `Warning` unsupported grant and no auth; `TokenUrl = accessTokenUrl`, `AuthorizationUrl = authUrl`, `ClientId = clientId`, `ClientSecret = clientSecret`, `Scope = scope`, `Audience = audience`, `Username = username`, `Password = password`, `RedirectUri = redirect_uri`, `ClientAuthentication = client_authentication == "body" ? Body : BasicHeader`, `Placement = addTokenTo == "queryParams" ? Query : Header`, `HeaderPrefix = headerPrefix` (default `Bearer`), `QueryParameterName = "access_token"` |
  | `jwt` | `Jwt` with `JwtSettings`: `algorithm` (HS256, HS384, HS512, RS256, RS384, RS512, ES256, ES384, ES512; unknown -> `Warning` and HS256), `secret`, `isSecretBase64Encoded` (`true`), `privateKey`, `payload` (default `{}`), `header` -> ExtraHeaders, `addTokenTo` `queryParams` -> Query else Header, `headerPrefix` (default `Bearer`), `queryParamKey` (default `token`), `AddIssuedAt` true, `ExpiresInSeconds` 0 |
  | anything else (`ntlm`, `hawk`, `oauth1`, `edgegrid`, `akamai`, `asap`, `bearer` variants unknown) | `RequestAuth.None` and `Warning` `Strings.ImportAuthUnsupported(path, type)` |

  Missing keys are empty strings. Dynamic variables are translated in every string value (`TranslateDynamicVariables`). Secret literals stay as written (Yaapic auth fields are plain text; the report adds **one** `Info` per import,
  `Strings.ImportAuthPlainText`, when any auth carries a non-empty value that is not a `{{variable}}`: "Credentials of the imported requests are stored as plain text in the workspace files. Move them to secret variables.").
- **Inheritance** (ADR 0016): the walk keeps the nearest ancestor's mapped auth (collection `auth`, then each folder `auth` replacing it, `noauth` included). A request without `auth` gets the inherited one (a copy of the record); when it came from an
  ancestor and is not `None`, count it and add **one** `Info` per import `Strings.ImportAuthInherited(count)`. A request with its own `auth` ignores the ancestors. The collection folder and the folders themselves carry no auth (the model has none).
- `PostmanItems` (M5-12) is extended: the walk passes the inherited auth down and sets `HttpRequestDefinition.Auth`.
- Strings (EN and FR): `import.authUnsupported` = "Authentication type \"{1}\" is not supported; the request was imported without authentication."; `import.authPlainText`; `import.authInherited` = "{0} requests got the authentication of their folder or collection copied onto them.".

## Out of scope

Scripts, variables, bodies.

## Acceptance criteria

- [x] Tests: one case per type of the table with the exact `RequestAuth` produced (including the OAuth 2 grants, client authentication, token placement, the JWT defaults and `isSecretBase64Encoded`), v2.0 object form, array form, unknown grant, unknown JWT algorithm, `ntlm`, absent auth,
  inheritance (collection -> folder -> request, folder `noauth` stops the inheritance, request override), the aggregated entries (inherited count, plain-text `Info` only when a literal credential is present), a `{{token}}` value does not trigger the plain-text `Info`.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): import Postman authentication`
