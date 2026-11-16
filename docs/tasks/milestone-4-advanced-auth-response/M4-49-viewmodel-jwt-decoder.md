# M4-49: ViewModel: JWT decoder

Status: todo
Platform: any
Depends on: M4-14

## Goal

A view model for the "Decode JWT" dialog: paste a token, see header, payload and expiry.

## Design decisions

- Read first: ADR 0013 (JWT decoding), M4-14, an existing small dialog view model (for example the script converter view model from U-05).
- `JwtDecoderViewModel(TimeProvider time)` in `Yaapic.ViewModels.Tools` (new folder):
  - `Input` (observable, the pasted text), recomputed on every change.
  - Outputs: `bool IsValid`, `string ErrorText` (localized: `Strings.JwtDecodeError*` per failure reason: not three parts, bad Base64, header or payload not
    JSON object), `HeaderText`, `PayloadText` (pretty JSON from the decoder), `SignatureText` (`Strings.JwtSignatureNotVerified` + the encoded signature,
    or `Strings.JwtUnsigned`), `AlgorithmText`, `IReadOnlyList<JwtClaimRow> Claims` for `iat`, `nbf`, `exp` (`JwtClaimRow(string Name, string
    LocalTimeText, string RelativeText)` such as `in 2 h` / `3 days ago`), `StatusText` and `StatusKind` (`Valid`, `Expired`, `NotYetValid`, `NoExpiry`, `None`).
  - `InsertFromClipboardCommand` is **not** here (the view calls `Input = text`); `ClearCommand` resets.
  - The signature is never verified; a permanent note `Strings.JwtNotVerifiedNote` is exposed as a property.
- Relative time formatting: a small pure helper `RelativeTime.Format(TimeSpan)` in `Yaapic.ViewModels` (seconds, minutes, hours, days; "in"/"ago"),
  localized with `Strings`.
- Strings (EN and FR): all the keys above.

## Out of scope

Dialog views (M4-65, M4-72), palette entry (M4-60), decoding from a response.

## Acceptance criteria

- [ ] Tests in `Yaapic.ViewModels.Tests` with a fake `TimeProvider`: valid token fills every output; each error; `Bearer ` prefix accepted; expiry
  states and relative texts at the boundaries; unsigned token; empty input gives no error and `StatusKind.None`; clear.
- [ ] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(viewmodels): add the JWT decoder view model`
