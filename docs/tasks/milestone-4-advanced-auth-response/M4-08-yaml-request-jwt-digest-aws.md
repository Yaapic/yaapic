# M4-08: YAML request serializer: JWT, Digest and AWS Signature v4

Status: todo
Platform: any
Depends on: M4-06, M4-07

## Goal

JWT, Digest and AWS Signature v4 authentications round-trip through the request YAML file.

## Design decisions

- Read first: M4-07 and its tests (same conventions), `RequestYamlSerializer.cs`.
- `auth.type` gains `jwt`, `digest`, `awsv4`.
- `digest` writes the existing `username` and `password` keys of `auth` (no new mapping).
- `jwt` writes a nested `auth.jwt` mapping:
  ```yaml
  auth:
    type: jwt
    jwt:
      algorithm: HS256                  # HS256 HS384 HS512 RS256 RS384 RS512 ES256 ES384 ES512
      secret: "{{jwtSecret}}"
      secretIsBase64: false
      privateKey: ""
      payload: '{"sub":"{{user}}"}'
      headers: ""
      addIssuedAt: true
      expiresIn: 300                    # seconds, 0 = no exp
      placement: header                 # header | query
      headerPrefix: Bearer
      queryParameter: token
  ```
  The multi-line `payload` and `privateKey` use YAML block scalars when they contain a newline (check how M3-05 writes script source and
  reuse `ScriptSourceFormatter` style handling, or the existing multi-line mechanism).
- `awsv4` writes `auth.awsv4` with `accessKeyId`, `secretAccessKey`, `sessionToken`, `region`, `service`.
- Same tolerance rules as M4-07 (missing keys read as defaults, unknown algorithm text reads as `HS256`, nested mapping of another type
  ignored).
- `docs/file-format.md` documents the three shapes.

## Out of scope

Resolution and signing, UI.

## Acceptance criteria

- [ ] Tests in `Yaapic.Infrastructure.Tests`: round trip of each kind and of each JWT algorithm; a PEM private key with newlines and a
  payload with quotes survive; defaults; unknown algorithm; digest keeps its username and password; stable output.
- [ ] Docs updated.
- [ ] Architecture tests green, 0 warnings, all tests green.

## Suggested commit message

`feat(storage): persist JWT, Digest and AWS Signature v4 authentication`
