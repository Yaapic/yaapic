# M4-01: ADR 0013: advanced authentication

Status: done
Platform: any
Depends on: none

## Goal

Write ADR 0013, the reference for every authentication card of milestone 4 (OAuth 2.0, JWT, Digest, AWS Signature v4). Docs only. The
decisions below are final.

## Design decisions (to record in the ADR)

- **Kinds.** `AuthKind` gains `OAuth2`, `Jwt`, `Digest`, `AwsV4` (file values `oauth2`, `jwt`, `digest`, `awsv4`). `RequestAuth` keeps
  its positional constructor and gains three init properties, `OAuth2` (`OAuth2Settings?`), `Jwt` (`JwtSettings?`), `AwsV4`
  (`AwsV4Settings?`), all `null` by default. Digest reuses the existing `Username` and `Password` members and has no settings record.
  Auth stays per request in M4 (inheritance from folders and collections is the blocked card M4-28).
- **Variables.** Every string field of every kind accepts `{{variables}}`; unresolved names are reported like today. As in ADR 0010 the
  fields are plain text in the YAML file: the supported protection is `{{name}}` plus a secret variable, and the editors show a hint when a
  secret-like field (client secret, password, private key, secret access key) holds a literal.
- **Pipeline.** `RequestResolver` still builds Basic, Bearer and API key synchronously. For the new kinds it substitutes the variables and
  stores a `ResolvedAuth` in `ResolvedRequest.PendingAuth`. `RequestSender` then calls `IRequestAuthenticator.AuthenticateAsync` (an
  optional constructor argument; `null` keeps today's behaviour) which returns the request with its auth headers or query parameter added, or
  an error message. An error becomes `SendFailureKind.AuthenticationFailed`. Rule of M2-06 kept: an enabled user header with the same name
  wins and the auth header is not added. Scripts run before this stage, so a pre-request script can still change the auth-related
  variables; `yaapic.sendRequest` requests carry no auth definition.
- **OAuth 2.0 grants.** `ClientCredentials`, `AuthorizationCode` (always with PKCE S256, plus the client secret when one is set) and
  `Password`. Refresh tokens are used automatically when a token expired and a refresh token exists. Not supported: Implicit (removed by
  OAuth 2.1), Device Code, JWT-bearer assertions, DPoP, mTLS-bound tokens. Client authentication at the token endpoint: `BasicHeader`
  (default) or `RequestBody`. Token placement: `Header` (`Authorization: <prefix> <token>`, default prefix `Bearer`) or `Query` (default
  parameter `access_token`). Optional `Scope` and `Audience` parameters.
- **Token lifetime.** Tokens live in memory only (`IOAuth2TokenCache`), keyed by a SHA-256 of grant, token URL, client id, scope, audience
  and username; they are never written to the workspace, the history or the vault, never logged and never shown in the console. A token
  counts as expired 30 seconds before its `expires_in` (no `expires_in` means no expiry). The user can clear a token from the auth tab.
- **When tokens are fetched.** `ClientCredentials` and `Password` fetch (or refresh) automatically on send. `AuthorizationCode` never opens
  a browser during a send: without a valid token the send fails with "Get a new access token first"; the user presses the "Get new access
  token" button of the auth tab (interactive, cancelable).
- **Authorization-code redirect.** A loopback listener (`TcpListener` on `127.0.0.1`, a tiny hand-written HTTP parser, no `HttpListener`,
  no embedded web view). The redirect URI is a user field, default `http://127.0.0.1:8742/callback`; its host must be `127.0.0.1` or
  `localhost`, its port is the listening port. `state` is a random 128-bit value and is checked; the wait times out after 3 minutes; the
  browser gets a static "You can close this tab" page. The system browser is opened through `Process.Start` with `UseShellExecute` and only
  for `http` and `https` URLs.
- **JWT** (signing a token for the request, the "JWT Bearer" auth of Postman): algorithms HS256/384/512, RS256/384/512, ES256/384/512.
  HS uses `Secret` (UTF-8, or base64 when `SecretIsBase64`); RS and ES use `PrivateKey` as PEM text (`RSA.ImportFromPem`,
  `ECDsa.ImportFromPem`). `Payload` is a JSON object text (variables substituted, must parse to an object) and `ExtraHeaders` an optional
  JSON object merged into the header (`alg` and `typ` are always set by the signer). `AddIssuedAt` adds `iat` and `ExpiresInSeconds` > 0
  adds `exp`, only when the payload does not define them. Placement `Header` (`Authorization: <prefix> <jwt>`, prefix `Bearer`) or `Query`
  (default parameter `token`). A new token is signed on every send (no cache). Not supported: PS256, EdDSA, JWE, key files.
- **JWT decoding** (a separate helper): `JwtDecoder` splits and decodes header and payload without verifying the signature and reports
  `exp`/`nbf`/`iat` status. Surfaced as a "Decode JWT" dialog (M4-65, M4-72), never automatic.
- **Digest** (RFC 7616): a probe request is sent first, with the same method, URL and headers but **without a body** (so a POST is not
  executed twice) and without `Authorization`; the `WWW-Authenticate: Digest` challenge of the 401 answer is parsed; the real request is
  then sent with the `Authorization` header. Algorithms MD5, MD5-sess, SHA-256, SHA-256-sess; `qop=auth` or no qop; a server offering only
  `auth-int` is an error. If the probe does not return 401 with a Digest challenge, the real request is sent unchanged. `nc` is
  `00000001` (one request per nonce), `cnonce` is random.
- **AWS Signature v4** (headers form only, no presigned URLs): fields `AccessKeyId`, `SecretAccessKey`, optional `SessionToken`, `Region`,
  `Service`. Signed headers: `host`, `x-amz-date`, `x-amz-content-sha256` and `x-amz-security-token` (added), plus `content-type` when
  present. Payload hash: SHA-256 of the UTF-8 text body (empty body: hash of empty), SHA-256 of the file content for a binary body,
  `UNSIGNED-PAYLOAD` for multipart bodies (documented limit). The clock is a `TimeProvider`.
- **Secrets in transit.** Authenticators never log values; failure messages never contain the token, secret or key.
- **Native AOT.** Only `System.Security.Cryptography`, `System.Text.Json` source generators and `HttpClient`; no reflection, no package.

## Out of scope

Code, UI, NTLM, Kerberos, Hawk, OAuth 1.0, persisting tokens (a later vault-backed card), request-level auth scripts.

## Acceptance criteria

- [x] ADR 0013 (`docs/adr/0013-advanced-authentication.md`) contains every decision above and the accepted limits; `docs/adr/README.md`
  lists it.
- [x] `docs/file-format.md` gets a short "Authentication (milestone 4)" section stating that the YAML shape is defined by M4-07 and M4-08.
- [x] No code changed; `dotnet build Yaapic.sln -c Release` still has 0 warnings.

## Suggested commit message

`docs(adr): add ADR 0013 advanced authentication`
