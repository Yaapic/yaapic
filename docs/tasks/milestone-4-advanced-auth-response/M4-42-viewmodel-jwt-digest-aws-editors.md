# M4-42: ViewModel: JWT and AWS Signature v4 editors

Status: todo
Platform: any
Depends on: M4-06, M4-40

## Goal

View models edit the fields of `JwtSettings` and `AwsV4Settings` (Digest uses the existing username and password fields).

## Design decisions

- Read first: ADR 0013, M4-40 (same pattern: `_loading`, `Changed`, `Load`, `To...`), `AuthEditorViewModel.cs`.
- `JwtEditorViewModel` in `Yaapic.ViewModels.Requests`: observable properties for every field of `JwtSettings`; static lists `Algorithms` and
  `Placements`; flags `UsesSecret` (HS algorithms), `UsesPrivateKey` (RS and ES), `ShowQueryParameter` / `ShowHeaderPrefix` by placement; `Hints`:
  literal-secret hints for `Secret` (HS) and `PrivateKey` (RS/ES) as in M4-40, `Strings.JwtPayloadInvalid` when `Payload` is non-empty, has no
  `{{` and is not a JSON object (check with `JsonDocument.Parse` in a try/catch), the same for `ExtraHeaders`.
  `Load` and `ToSettings` round-trip; `Changed` semantics as M4-40.
- `AwsV4EditorViewModel`: observable `AccessKeyId`, `SecretAccessKey`, `SessionToken`, `Region`, `Service`; `Load`, `ToSettings`, `Changed`;
  `Hints` for a literal `SecretAccessKey` and `SessionToken`; `Strings.AwsFieldMissing(name)` hints are **not** shown while the user types
  (only the sender reports them).
- Strings for labels, algorithm and placement names, EN and FR, regenerate `Strings.g.cs`.

## Out of scope

Wiring into `AuthEditorViewModel` (M4-43), views, signing.

## Acceptance criteria

- [ ] Tests in `Yaapic.ViewModels.Tests`: round trips; `Changed` rules; flags per algorithm and placement; hints with literal, `{{var}}` and
  empty values; invalid JSON payload and headers hints; a payload containing `{{x}}` is not flagged.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(viewmodels): add the JWT and AWS Signature v4 editor view models`
