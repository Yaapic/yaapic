# M4-80: Milestone 4 docs

Status: done
Platform: any
Depends on: M4-78, M4-79, M4-62, M4-63, M4-64, M4-65, M4-66, M4-67, M4-68, M4-69, M4-70, M4-71, M4-72, M4-73, M4-74, M4-75, M4-76, M4-77

## Goal

The documentation matches what milestone 4 delivered, and the task index records the milestone summary.

## Design decisions

- Read first: the milestone 3 docs card (M3-67) and the "Milestone 3 summary" block in `docs/tasks/README.md`, ADRs 0013 to 0015, `docs/spikes/m4-timings.md`.
- `docs/architecture.md`: the authentication pipeline (resolver, `PendingAuth`, authenticators, token service), the transport additions (certificate provider, timing collector, HTTP version),
  the diff and code generation services; a small component diagram in the existing style.
- `docs/file-format.md`: final shapes of `auth.oauth2`, `auth.jwt`, `auth.awsv4`, `auth.digest`, `settings.httpVersion`, and a note that certificates and tokens are not in the workspace.
- `docs/dependencies.md`: no new package expected; confirm and state it (and any external tool the tests use optionally: `curl`, `node`, `python`, `pwsh`, `gofmt`).
- A new `docs/authentication.md` (user level): each kind with its fields, the variable and secret advice, the OAuth 2.0 flow with the redirect URI and the loopback port, the Digest probe behaviour,
  AWS limits (multipart is unsigned payload), JWT algorithms, troubleshooting messages.
- A new `docs/response-tools.md`: timings (what each phase means, reused connections, `n/a`), diff (what is compared, the options, limits), code generation (languages, the secrets warning), JWT decoder.
- `README.md` (repository root): the feature list gains the M4 items.
- `docs/tasks/README.md`: add the "Milestone 4 summary" block after the card table (delivered, deferred, open items, runtime checks not run) in the style of the Milestone 3 summary; the milestone
  table row for M4 says `detailed (81 cards, see above)`.

## Out of scope

Screenshots (M4-81).

## Acceptance criteria

- [x] The documents above are updated and their links work (check relative links by opening each).
- [x] The summary lists the blocked cards still open (M4-28, M4-37, M4-55) and anything deferred.
- [x] No code changed; the build still has 0 warnings.

## Suggested commit message

`docs: document the milestone 4 features`
