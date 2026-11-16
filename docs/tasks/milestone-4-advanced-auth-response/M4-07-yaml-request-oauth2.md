# M4-07: YAML request serializer: OAuth 2.0

Status: todo
Platform: any
Depends on: M4-05

## Goal

An OAuth 2.0 authentication round-trips through the request YAML file.

## Design decisions

- Read first: `src/Yaapic.Infrastructure/Storage/RequestYamlSerializer.cs` (`AuthTypeText`, `ReadAuth`), `RequestFileDto.cs`,
  `docs/file-format.md`, and how M3-05 added the `scripts` section (same style: optional, omitted when default).
- `auth.type` gains `oauth2`. A nested `auth.oauth2` mapping, written only for `oauth2`:
  ```yaml
  auth:
    type: oauth2
    oauth2:
      grant: client_credentials        # client_credentials | authorization_code | password
      tokenUrl: https://idp.example.com/token
      authorizationUrl: ""             # authorization_code only
      clientId: "{{clientId}}"
      clientSecret: "{{clientSecret}}"
      scope: read write
      audience: ""
      username: ""                     # password grant only
      password: ""
      redirectUri: http://127.0.0.1:8742/callback
      clientAuth: basic                # basic | body
      placement: header                # header | query
      headerPrefix: Bearer
      queryParameter: access_token
  ```
- Empty strings may be omitted when writing; a missing key reads as the `OAuth2Settings.Default` value. An unknown `grant`, `clientAuth` or
  `placement` value reads as the default and does not throw (same tolerance as the existing unknown auth type: check `ReadAuth` and copy
  its behaviour). `auth.oauth2` on a different `type` is ignored on read.
- DTO classes stay source-generator friendly (VYaml, no reflection), registered where the existing DTOs are.
- Output is deterministic (stable key order) so Git diffs stay small.
- `docs/file-format.md`: document the `oauth2` shape and the plain-text rule (use `{{variables}}` for `clientSecret` and `password`).

## Out of scope

JWT, Digest, AWS (M4-08), folder or collection level auth.

## Acceptance criteria

- [ ] Tests in `Yaapic.Infrastructure.Tests`: round trip of each grant with every field set; defaults when keys are missing; unknown
  enum text tolerated; a file written by the previous version (no `oauth2`) still reads; the four old kinds unchanged; stable output.
- [ ] Docs updated.
- [ ] Architecture tests green, 0 warnings, all tests green.

## Suggested commit message

`feat(storage): persist OAuth 2.0 authentication in the request YAML`
