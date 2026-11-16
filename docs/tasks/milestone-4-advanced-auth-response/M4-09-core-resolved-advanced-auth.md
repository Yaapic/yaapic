# M4-09: Core: resolved advanced authentication

Status: todo
Platform: any
Depends on: M4-06

## Goal

`RequestResolver` substitutes variables in the OAuth 2.0, JWT, Digest and AWS settings and hands the result to the sender as
`ResolvedRequest.PendingAuth`.

## Design decisions

- Read first: ADR 0013 (Pipeline), `src/Yaapic.Core/Http/RequestResolver.cs` (`ApplyAuth`, `Sub`, how `Unresolved` is collected),
  `ResolvedRequest.cs`.
- `public sealed record ResolvedAuth(AuthKind Kind, string Username, string Password, OAuth2Settings? OAuth2, JwtSettings? Jwt,
  AwsV4Settings? AwsV4)` in `Yaapic.Core.Http`. Every string inside is already substituted.
- `ResolvedRequest` gains a last optional parameter `ResolvedAuth? PendingAuth = null` (record positional, default null, so existing
  callers and tests compile).
- In `RequestResolver.Resolve`: for `OAuth2`, `Jwt`, `Digest`, `AwsV4` kinds (with their settings non-null; a null settings object is
  treated as the `Default`), build `PendingAuth` with every string field passed through `Sub`; unresolved names are added to `Unresolved`
  exactly like the old kinds. For the four old kinds and `None`, `PendingAuth` stays null and behaviour is unchanged.
- The resolver adds no header or query itself for the new kinds.
- Scripted sends: `ScriptedRequestSender` builds its input through the same resolver path; check that a pre-request script cannot read
  `PendingAuth` (it is not part of `ScriptRequest`). No change expected, state the check in the Notes of the card.

## Out of scope

The authenticator port and the send pipeline (M4-10), any signing or network call.

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests`: each new kind produces the right `PendingAuth` with variables substituted in every field; unresolved
  names reported once; old kinds produce `PendingAuth == null` and the same headers as before; no auth header added for the new kinds.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): resolve advanced authentication settings`
