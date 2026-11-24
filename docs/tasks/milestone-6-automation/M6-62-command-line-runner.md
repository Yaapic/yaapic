# M6-62: Command-line runner

Status: blocked
Platform: any
Depends on: M6-17, M6-18, M6-19

## Goal

Run a folder of a workspace from a terminal or a CI pipeline, without the window, and get a JUnit or JSON report (a "Newman" for Yaapic).

## Open question (product decision, answer before picking this card)

The runner core (M6-14 to M6-19) has no UI dependency, so a headless runner is mostly packaging. What should it be?

1. **A separate `yaapic-cli` executable** (new project `Yaapic.Cli`, Native AOT, references Composition without any App project):
   `yaapic-cli run <workspace> --folder "Collection/Sub" [--environment name] [--iterations n] [--data file] [--delay ms]
   [--stop-on-failure] [--reporter junit|json] [--output file]`, exit code 0 when every request passed, 1 otherwise. Needs its own
   packaging (zip per OS on GitHub Releases, not Velopack), script engines shipped next to it, and a decision on secrets in CI (the vault is
   per user and machine: environment variables `YAAPIC_VAR_<name>` overriding variables?).
2. **The same executable with a verb**: `Yaapic.exe run ...` on Windows and the AppImage/bundle with `run` on Linux/macOS. No second
   package, but GUI subsystem executables on Windows have no console attached (output needs `AttachConsole`) and startup is heavier.
3. **A .NET global tool** (`dotnet tool install yaapic`): easy in CI with the SDK, but a tool is not Native AOT by default and needs the
   SDK on the agent.
4. **Not now** (the in-app Runner and its JUnit export cover the first need).

Also to decide: how secrets reach a headless run (environment variables, a passphrase-protected vault file, or no secrets).

Until answered, no other card depends on this one.

## Acceptance criteria

- [ ] Rewritten after the answer; nothing to implement as is.

## Suggested commit message

`docs: record the decision on the command-line runner`
