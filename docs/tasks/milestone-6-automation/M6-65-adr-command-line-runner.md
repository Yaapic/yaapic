# M6-65: ADR 0022: command-line runner

Status: todo
Platform: any
Depends on: M6-62

## Goal

Write ADR 0022 from the decision recorded in M6-62, so the CLI cards (M6-66 to M6-74) have one reference, and point ADR 0018 and ADR 0011 at it.

## Design decisions

- Read first: M6-62 (the decision and the secrets default), ADR 0018 (Runner), ADR 0010 (vault), ADR 0011 (distribution), `docs/adr/README.md`
  (format: status, options considered, Native AOT risks, decision).
- `docs/adr/0022-command-line-runner.md`, status `Accepted`. Sections:
  - **Context**: CI needs to run a folder without the window; the Runner core (`Yaapic.Core.Runner`) has no UI dependency.
  - **Options considered**: the four options of M6-62 (separate exe chosen; verb on the app exe, .NET tool, not now rejected, with the
    reasons of M6-62); for secrets: environment variables (chosen as a default), vault file plus passphrase, no secrets.
  - **Native AOT risks**: none new; no package (the argument parser is hand-written, no `System.CommandLine`); the CLI reuses the AOT-clean
    Core, Infrastructure, Scripting and Composition code.
  - **Decision**, copy these rules exactly:
    - Projects: `src/Yaapic.Cli` (assembly `yaapic-cli`, `OutputType` `Exe`, Native AOT) may reference `Composition`, `Core`, `Domain`,
      `Infrastructure`, `Localization`. Wiring in `Yaapic.Composition.HeadlessComposition`. Read-only run sources in Core: `StaticRunScopeSource`,
      `TreeScriptChainProvider`, `RunPlanBuilder.FindFolder`; overrides in `VariableOverrides`; masking in `FixedSecretRedactor`.
    - Command line and options (table: option, value, default, range): `run <workspace>` (required, an existing directory), `--folder`
      (required, `/`-separated folder names, first segment = collection, ordinal case-sensitive), `--environment` (environment name, ordinal;
      default none), `--iterations` (1 to 10 000; default the data row count with `--data`, else 1), `--data` (CSV or JSON file, ADR 0018
      rules), `--delay` (0 to 60 000 ms, default 0), `--stop-on-failure` (flag), `--reporter` (`junit` or `json`), `--output` (file path;
      requires `--reporter`; default: the report's suggested file name in the current directory). `--opt value` and `--opt=value` both work;
      options may come in any order after the workspace. `--help`/`-h` and `--version` anywhere print and exit 0.
    - Exit codes: 0, 1, 2, 3, 4, 130 with the meanings of M6-62. Precedence: 130 (canceled) > 3 (report could not be written) > 1 > 0.
    - Read-only run: `PersistVariables` is always `false`; no history; the workspace is never created or modified (`Workspace.EnsureCreated`
      is not called, the session file and `.yaapic/` are not read or written); the active environment of the app is ignored.
    - Secrets: the default of M6-62, marked "default, may be revisited".
    - Authentication: everything that needs no user interaction works; the OAuth 2.0 authorization-code flow fails the request with
      "yaapic-cli cannot open a browser: the OAuth 2.0 authorization code flow needs an interactive sign-in. Use client credentials or
      password, or pass a token with YAAPIC_VAR_<name>." Client certificates come from the per-user certificate list as in the app; a
      certificate whose passphrase is in the vault cannot be loaded (the send errors with the loader's message).
    - Output: plain text, English only (CI logs; the app's FR/EN catalogue is not used), progress and summary on stdout, warnings and
      errors on stderr.
    - Distribution: `yaapic-cli-<version>-<rid>.zip` per OS (`win-x64`, `linux-x64`, `osx-arm64`) on the GitHub Release, with the native
      script engines next to the executable; checksums in the existing `SHA256SUMS-<rid>.txt`; no Velopack, no auto-update; unsigned.
  - **Out of scope**: running from the vault, `--var` on the command line, HTML or several reporters at once, choosing requests inside the
    folder, running load tests, mock servers or monitors headless, an installer or package-manager package (winget, Homebrew, apt).
- `docs/adr/README.md`: add the row `| [0022](0022-command-line-runner.md) | Command-line runner | Accepted |`.
- ADR 0018, "Out of scope for the milestone": replace "A command-line runner (blocked card M6-62)" with "A command-line runner (ADR 0022)".
- ADR 0011: add one sentence under the decision: "The command-line runner is not packed by Velopack: it ships as a plain zip per OS on the
  same GitHub Release (ADR 0022)."
- ADR 0021, option "An operating-system scheduled task": "a headless entry point that does not exist (see blocked card M6-62)" becomes "a
  headless entry point (`yaapic-cli`, ADR 0022, did not exist when this ADR was written)". Change nothing else in ADRs 0011, 0018, 0021.

## Out of scope

Code; user docs (M6-74).

## Acceptance criteria

- [ ] `docs/adr/0022-command-line-runner.md` exists with the sections and rules above; the ADR index lists it.
- [ ] ADRs 0011, 0018 and 0021 carry the one-line changes above and nothing else.
- [ ] Markdown links resolve (check by opening each relative link target).

## Suggested commit message

`docs(adr): add ADR 0022 for the command-line runner`
