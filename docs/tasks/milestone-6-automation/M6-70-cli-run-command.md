# M6-70: CLI: the run command

Status: todo
Platform: any
Depends on: M6-68, M6-69

## Goal

`yaapic-cli run` loads the workspace read-only, finds the folder, the environment and the data file, runs the folder through
`HeadlessComposition` and returns the ADR 0022 exit code with a one-line summary. Live output, reports and Ctrl+C come in M6-71.

## Design decisions

- Read first: ADR 0022, ADR 0018 ("Options", "Iteration data"), M6-66 to M6-69, `src/Yaapic.Core/Runner/RunPlan.cs`, `RunOptions.cs`,
  `RunSummary.cs`, `IterationDataReader.cs`, `src/Yaapic.Core/Import/ImportInput.cs` (`Single.Text`), `FileImportInputReader`
  (throws `ImportException`).
- `src/Yaapic.Cli/RunCommand.cs`: `internal static class RunCommand { public static Task<int> ExecuteAsync(RunArguments args, TextWriter stdout,
  TextWriter stderr, IReadOnlyDictionary<string, string?> environment, CancellationToken ct) }`, called by `CliProgram` for a valid `run`
  (remove the M6-69 "not implemented" branch and its test).
- Steps, in this order; each `error:` line goes to stderr and returns the code shown:
  1. `root = Path.GetFullPath(args.Workspace)`; not an existing directory: `error: workspace '{root}' does not exist` (3). Nothing is created.
  2. `overrides = VariableOverrides.FromEnvironment(environment)`; `using var services = HeadlessComposition.Create(root, overrides)`.
  3. `tree = await services.Collections.LoadTreeAsync(ct)`; `folder = RunPlanBuilder.FindFolder(tree, args.Folder)`; null:
     `error: no folder '{args.Folder}' in the workspace` (3). `items = RunPlanBuilder.ListRequests(folder)`; empty:
     `error: the folder '{args.Folder}' has no requests` (3).
  4. With `--environment`: environments whose `Name` equals it (ordinal). None: `error: no environment named '{name}'` (3); more than one:
     `error: {n} environments are named '{name}'` (3). Without it: `null` (the app's active environment is never read).
  5. `globals = await services.Globals.LoadAsync(ct)`.
  6. With `--data`: `input = await services.Files.ReadFileAsync(path, ct)`, `data = IterationDataReader.Read(Path.GetFileName(path),
     input.Single.Text)`; an `ImportException` or `IterationDataException`: `error: data file '{path}': {message}` (3).
  7. Iterations: `args.Iterations ?? (data is { Rows.Count: > 0 } ? Math.Min(data.Rows.Count, 10000) : 1)`.
  8. Missing secrets: `VariableOverrides.FindEmptySecrets` over the environment rows, the variables of the top-level folder that contains
     the run folder, and the globals, minus the override names; for each, on stderr:
     `warning: secret variable '{name}' has no value; set YAAPIC_VAR_{name} (yaapic-cli does not read the vault)`.
  9. `plan = new RunPlan(folder.Name, items, new RunOptions(iterations, args.DelayMs, args.StopOnFailure, PersistVariables: false), data?.Rows ?? [])`;
     `result = await services.CreateRunner(tree, environment, globals).RunAsync(plan, null, ct)`.
  10. stdout, one line: `{status}: {sent} sent, {skipped} skipped, {errors} errors, {failed} failed; tests {passed} passed, {testsFailed} failed`
      where `{status}` is the lower-case camel name of `RunStatus` (as in the JSON report) and the counts come from `RunSummary`. When the
      run has an error text (`RunResult` error), a second line `run error: {text}` goes to stderr.
- Exit code: `Canceled` status: 130; else 1 when the status is `StoppedOnFailure` or `Failed` or any request result in any iteration is
  `Failed` or `Error`; else 0 (`Completed` or `Stopped` with only `Passed`/`Skipped` requests).
- A `StorageException` while loading (corrupt YAML): `error: {message}` (3). Any other exception escapes to `Program.cs` (4).

## Out of scope

Per-request lines, reports, Ctrl+C (M6-71); packaging (M6-73).

## Acceptance criteria

- [ ] `tests/Yaapic.Cli.Tests/RunCommandTests.cs` calling `CliProgram.RunAsync` with `StringWriter`s and a dictionary for the environment.
  Workspaces are written with the Infrastructure file stores into a temp directory; requests target a loopback server: link
  `tests/Yaapic.Infrastructure.Tests/EndToEnd/LoopbackServer.cs` and `TempWorkspace.cs` into the test project
  (`<Compile Include="..." Link="Shared\..." />`; if they depend on other test files, link those too or copy the few lines needed).
  Cases: missing workspace (3, nothing created on disk); unknown folder (3); empty folder (3); unknown and duplicated environment names (3);
  missing and invalid data file (3); two passing requests (0, summary line); a failing test (1); a send error to a closed port (1); a data file
  with 3 rows and no `--iterations` runs 3 iterations; `--iterations 2` with the same file runs 2; a `YAAPIC_VAR_token` value reaches the
  request (echo) and a secret environment row without override prints the warning; `setNextRequest(null)` after a passing request gives 0
  with status `stopped`.
- [ ] After every case the workspace tree is unchanged (snapshot before and after).
- [ ] Architecture tests green, 0 warnings, all tests green; AOT publish of `Yaapic.Cli` still has 0 IL2xxx/IL3xxx warnings.
- [ ] Docs: none in this card (M6-74).

## Suggested commit message

`feat(cli): run a folder from the command line`
