# R-09: Update end-to-end tests and docs

Status: todo
Platform: any
Depends on: R-06, R-07, R-08

## Goal

Close the distribution milestone: end-to-end coverage, user-facing docs, README install section.

## Design decisions

- End-to-end test through the shell ViewModel with the fake service: startup check, banner, download, restart-to-apply,
  channel switch, offline failure.
- README gets an "Install" section (Windows Setup, macOS, Linux AppImage, how updates work, how to disable them) and the
  contributor section links `docs/releasing.md`.
- `docs/dependencies.md` lists Velopack (version, licence, why).

## Out of scope

Store distribution.

## Acceptance criteria

- [ ] Tests green on the three OSes.
- [ ] Task index updated, milestone marked done with what was not checked by hand.

## Suggested commit message

`docs: document installation and updates, add end-to-end tests`
