# M6-74: Command-line runner docs

Status: todo
Platform: any
Depends on: M6-72, M6-73

## Goal

Document `yaapic-cli` for users (install, options, exit codes, secrets, CI examples) and remove the "not delivered" mention of the
command-line runner.

## Design decisions

- Read first: ADR 0022, M6-62 (the secrets default and its revisit note), `docs/automation.md`, `README.md` ("Install", "Automation",
  "Repository layout"), `docs/architecture.md` ("Automation (milestone 6)"), `docs/tasks/README.md` (Milestone 6 summary), the final
  behaviour of M6-69 to M6-73 (copy the real texts, not this card).
- `docs/automation.md`: a new section `## Command line (yaapic-cli)` after "Monitors", with:
  - install: download `yaapic-cli-<version>-<rid>.zip` from the GitHub Release, unzip anywhere, keep the native libraries next to the
    executable; macOS: `xattr -d com.apple.quarantine yaapic-cli`; no automatic update;
  - the usage line and the options table of ADR 0022, the default iterations with a data file, folder paths (`Collection/Sub`, exact case);
  - what differs from the window: read-only (no variable saved, no history, nothing written to the workspace), no active environment (pass
    `--environment`), no vault;
  - **Secrets**: `YAAPIC_VAR_<name>`, precedence (above environment, collection and globals; below data and script locals), masking, the
    warning for empty secrets, why there is no `--var` option, and one sentence: "This is the current default; a vault-based option may come
    later." Examples for bash (`YAAPIC_VAR_token=... yaapic-cli run ...`), PowerShell (`$env:YAAPIC_VAR_token = ...`) and a GitHub Actions
    step using `env: YAAPIC_VAR_token: ${{ secrets.API_TOKEN }}` and publishing the JUnit file;
  - the output (one example run) and the exit code table;
  - authentication limits: OAuth 2.0 authorization code and certificates with a vault passphrase do not work headless;
  - in "Results and reports", replace "Yaapic has no command-line runner yet, so the file is exported by hand from the window (see "Not
    delivered")" with a link to the new section;
  - in "Not delivered in milestone 6", delete the "Command-line runner" bullet (keep the M6-63 and M6-64 bullets written by the
    decision commit).
- `README.md`: "Automation" paragraph: one sentence on `yaapic-cli` with a link to the new section; "Install": one line on the CLI zip.
- `docs/architecture.md`, "Automation (milestone 6)": a short "Headless runs" paragraph (`HeadlessComposition`, `StaticRunScopeSource`,
  `TreeScriptChainProvider`, `VariableOverrides`, `FixedSecretRedactor`, no vault, read-only).
- `docs/tasks/README.md`, Milestone 6 summary: add the CLI to "Delivered" (with `CliEndToEndTests`), and fix "Open"/"Deferred" so they
  match (no blocked card left in M6).
- `RELEASE_NOTES.md`: not touched (written at release time).
- `docs/dependencies.md`: unchanged (no package); check it does not need a line for the new project.

## Out of scope

Code.

## Acceptance criteria

- [ ] Every option, exit code and message quoted in the docs matches the code (copy from the help text and the tests).
- [ ] Relative links resolve; the GitHub Actions example is valid YAML.
- [ ] `docs/automation.md`, `README.md`, `docs/architecture.md` and the M6 summary updated as above; no remaining text says the
  command-line runner is missing or blocked (search for `M6-62` and "command-line runner" in `docs/` outside `docs/tasks/`, and in `README.md`).

## Suggested commit message

`docs: document the yaapic-cli command-line runner`
