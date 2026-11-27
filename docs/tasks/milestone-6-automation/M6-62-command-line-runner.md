# M6-62: Command-line runner

Status: done
Platform: any
Depends on: M6-17, M6-18, M6-19

## Goal

Run a folder of a workspace from a terminal or a CI pipeline, without the window, and get a JUnit or JSON report (a "Newman" for Yaapic).

## Decision (product owner, 2026-11-27)

Option 1 of the four proposed: a **separate `yaapic-cli` executable**.

- New project `src/Yaapic.Cli` (assembly `yaapic-cli`, Native AOT). It references `Yaapic.Composition`, `Yaapic.Core`, `Yaapic.Domain`,
  `Yaapic.Infrastructure` and `Yaapic.Localization` (to force English texts), never an App project. The headless wiring lives in `Yaapic.Composition` (`HeadlessComposition`), next to
  `AppComposition`.
- Command line:
  `yaapic-cli run <workspace> --folder "Collection/Sub" [--environment name] [--iterations n] [--data file] [--delay ms] [--stop-on-failure]
  [--reporter junit|json] [--output file]`, plus `--help` and `--version`.
- Exit codes: `0` every request passed (or was skipped), `1` at least one request failed or errored (or the run failed), `2` usage error,
  `3` input or I/O error (workspace, folder, environment, data file, report), `4` unexpected error, `130` canceled (Ctrl+C, SIGTERM).
- A headless run only **reads** the workspace: variable changes made by scripts live for the run only, nothing goes to the history, no file
  or folder is created in the workspace.
- Packaging: one zip per OS (`yaapic-cli-<version>-<rid>.zip`) on the GitHub Release, built by the release workflow, **not** Velopack (no
  installer, no automatic update); the QuickJS-NG and Lua native libraries ship next to the executable.

Rejected: the same executable with a `run` verb (a WinUI GUI-subsystem exe has no console; heavier start), a .NET global tool (not Native
AOT, needs the SDK on the agent), "not now".

### Secrets in CI: a default, open to revision

The owner did not decide how secrets reach a headless run. The cards use the **smallest safe default**, which the owner can still change:

- `yaapic-cli` never opens the vault (it is per user and machine, its protectors need DPAPI, a keychain or a passphrase prompt). Secret
  variables have an empty value in a headless run.
- An environment variable `YAAPIC_VAR_<name>` overrides variable `<name>` in the environment layer, so it wins over the environment,
  collection and globals values (data rows and script locals still win). Without `--environment` the overrides form an unnamed in-memory
  environment.
- Every override value is treated as a secret: masked (`••••`) in console lines and test messages, like vault values (same rule: values of at
  least four characters). Reports already hold URL templates only.
- Before the run, each enabled secret row that has no value and no override gets a warning on stderr; it is not an error.
- No `--var name=value` option: values on a command line show in process lists and CI logs.

Revisit when: a user needs the vault itself in CI (a passphrase-protected vault file read with a `YAAPIC_VAULT_PASSPHRASE`), or wants
non-secret overrides that stay visible in logs.

## Cards

ADR 0022 (M6-65), Core run sources (M6-66), variable overrides and redactor (M6-67), headless composition (M6-68), CLI project and
argument parser (M6-69), the run command (M6-70), output, reports and cancellation (M6-71), end-to-end tests (M6-72), packaging (M6-73),
docs (M6-74).

## Acceptance criteria

- [x] Rewritten after the answer; implementation split into M6-65 to M6-74.

## Suggested commit message

`docs: record the decision on the command-line runner`
