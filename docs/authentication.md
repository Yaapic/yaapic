# Authentication

Yaapic signs requests with Basic, Bearer, API key, OAuth 2.0, JWT, Digest and AWS Signature v4. Pick the kind in the **Auth**
tab of a request. Authentication is per request (there is no inheritance from folders or collections yet). The design is in
[ADR 0013](adr/0013-advanced-authentication.md); the file shapes are in [file-format.md](file-format.md).

## Variables and secrets

Every text field accepts `{{variables}}`. Auth fields are plain text in the request file, so never type a client secret,
password, private key or secret access key literally: define a **secret variable** (stored in the encrypted vault, see
[ADR 0010](adr/0010-secret-vault.md)) and write `{{name}}` in the field. The editors show a hint when such a field holds a
literal value. An enabled header you add yourself with the same name as the auth header (for example `Authorization`) wins, and
the auth header is not added. Scripts run before authentication, so a pre-request script can change the variables the fields use.

## Basic, Bearer, API key

Username and password (`Authorization: Basic ...`), a token (`Authorization: Bearer ...`), or a key and value sent as a header or
a query parameter.

## OAuth 2.0

| Field | Meaning |
| --- | --- |
| Grant | Client credentials, Authorization code (always with PKCE S256) or Password. |
| Access token URL | Absolute `http` or `https` URL of the token endpoint. |
| Authorization URL | Authorization code only. |
| Client ID, Client secret | The secret is optional for the authorization code grant; give it as a `{{variable}}`. |
| Scope, Audience | Optional parameters of the token request. |
| Username, Password | Password grant only. |
| Redirect URI | Authorization code only, default `http://127.0.0.1:8742/callback`. |
| Client authentication | Basic header (default) or in the request body. |
| Add token to | `Authorization` header (prefix `Bearer` by default) or a query parameter (`access_token` by default). |

Tokens are kept **in memory only**: never in the workspace, the history or the vault, and never logged. A token counts as
expired 30 seconds before its `expires_in`. When a token expired and a refresh token exists, it is refreshed automatically.
Use **Clear token** (or the palette entry "Clear OAuth 2.0 tokens") to forget it. Not supported: Implicit, Device Code,
JWT-bearer assertions, DPoP, mTLS-bound tokens.

- **Client credentials** and **Password**: the token is fetched (or refreshed) automatically when you send.
- **Authorization code**: a send never opens a browser. Without a valid token it fails with "Get a token first". Press
  **Get new token**: Yaapic opens the system browser on the authorization URL and waits on a loopback listener.
  - The redirect URI host must be `127.0.0.1` or `localhost`; its port is the port Yaapic listens on, so register exactly this
    URI with the identity provider.
  - `state` is a random 128-bit value and is checked. The wait ends after 3 minutes. The browser shows a "You can close this
    tab" page. The operation can be canceled.

## JWT (sign a token for the request)

Yaapic builds and signs a JWT on every send (no cache), like the "JWT Bearer" auth of Postman.

- Algorithms: HS256, HS384, HS512 (secret as UTF-8 text, or Base64 when "Secret is Base64 encoded" is ticked), RS256, RS384,
  RS512 and ES256, ES384, ES512 (private key as PEM text). Not supported: PS256, EdDSA, JWE, key files.
- **Payload** must be a JSON object. **Extra header fields** is an optional JSON object merged into the header; `alg` and `typ`
  are always set by Yaapic.
- "Add the issued-at claim" adds `iat`, and "Expires in" greater than 0 adds `exp`, only when the payload does not define them.
- Placement: `Authorization: <prefix> <jwt>` (prefix `Bearer`) or a query parameter (`token` by default).

To inspect a token, use **Decode JWT** (palette): see [response-tools.md](response-tools.md).

## Digest (RFC 7616)

Enter a username and a password. Yaapic first sends a **probe** with the same method, URL and headers but **no body** (so a POST
is not executed twice) and no `Authorization`, reads the `WWW-Authenticate: Digest` challenge of the 401 answer, then sends
the real request with the `Authorization` header. If the probe does not answer 401 with a Digest challenge, the real request is
sent unchanged.

Supported: MD5, MD5-sess, SHA-256, SHA-256-sess; `qop=auth` or no qop. A server that offers only `auth-int` is an error.
One request per nonce (`nc` is `00000001`).

## AWS Signature v4

Headers form only (no presigned URLs). Fields: access key ID, secret access key, optional session token, region and service.
The signed headers are `host`, `x-amz-date`, `x-amz-content-sha256`, `x-amz-security-token` (when a session token is set) and
`content-type` when present. The payload hash covers the text body or the content of a binary file; **multipart bodies are sent
with `UNSIGNED-PAYLOAD`** (a documented limit). The URL must be absolute.

## Troubleshooting

| Message | What to do |
| --- | --- |
| Get a token first: this grant needs an interactive sign-in. | Authorization code: press **Get new token**. |
| Enter an absolute http or https token URL. | Fix the Access token URL. |
| The redirect URI must be http://127.0.0.1:&lt;port&gt;/&lt;path&gt; or http://localhost:... | Use a loopback redirect URI. |
| The loopback port N is already in use. | Free the port, or change the redirect URI port (and the provider registration). |
| No redirect was received within 3 minutes. | Press **Get new token** again. |
| The token endpoint answered with an unexpected response (HTTP N). | Check URL, client id and secret, grant and scope; the answer must be JSON. |
| The JWT payload must be a JSON object. / The extra JWT headers must be a JSON object. | Fix the JSON. |
| The JWT secret or private key is empty. / The JWT secret is not valid Base64. | Fill the key, or untick "Secret is Base64 encoded". |
| Enter a username and a password for Digest authentication. | Fill both fields. |
| Digest qop 'auth-int' is not supported. | The server offers only `auth-int`; Digest cannot be used with it. |
| The AWS field is empty. | Fill the field (a variable that is not defined counts as empty). |
| Vault is locked: secret variables are not available. | Unlock the vault; secret variables stay unresolved until then. |
