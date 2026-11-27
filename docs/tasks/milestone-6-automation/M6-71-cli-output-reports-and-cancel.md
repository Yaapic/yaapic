# M6-71: CLI: live output, reports and cancellation

Status: todo
Platform: any
Depends on: M6-70

## Goal

Print one line per request while the run goes, write the JUnit or JSON report asked for with `--reporter`/`--output`, and stop cleanly on
Ctrl+C or SIGTERM (ADR 0022).

## Design decisions

- Read first: ADR 0022 (exit code precedence, output rules), M6-70, `src/Yaapic.Core/Runner/RunProgress.cs`, `CollectionRunner.RunAsync`
  (when progress is reported, what a canceled run returns), `RunReportJson.cs`, `RunReportJUnit.cs` (`Write`, `SuggestedFileName`).
- `src/Yaapic.Cli/ConsoleRunProgress.cs`: `internal sealed class ConsoleRunProgress(TextWriter stdout) : IProgress<RunProgress>`, writing
  **synchronously** in `Report` (do not use `Progress<T>`: it posts to the thread pool and lines could come out of order). Lines:
  - before the run (written by `RunCommand`): `Running '{args.Folder}': {items} requests x {iterations} iterations`;
  - when the iteration index changes and there is more than one iteration: `Iteration {i + 1}/{count}`;
  - for each new `Last` result: two spaces, the outcome padded to 5 (`PASS`, `FAIL`, `ERROR`, `SKIP`), a space, `{method} {path}/{name}`,
    then ` -> {status}` when there is a status code, ` ({ms} ms)` (whole milliseconds) unless skipped, and `: {error}` when there is an error;
  - then for each failed test of that result: eight spaces, `x {test name}: {message}` (`: {message}` omitted when empty).
  Test names, messages and errors are already redacted by the scripted sender; print them as they are.
- The summary line of M6-70 stays last on stdout.
- Reports (`RunCommand`, after the run, also for failed and canceled runs):
  - bytes: `RunReportJson.Write(result)` or `RunReportJUnit.Write(result)`;
  - path: `args.Output` (made absolute against the current directory) or `Path.Combine(currentDirectory, SuggestedFileName(result))`;
  - create the parent directory when missing; write with `File.WriteAllBytesAsync`;
  - success: stdout `Report ({json|junit}): {full path}`;
  - `IOException` or `UnauthorizedAccessException`: stderr `error: cannot write the report '{path}': {message}`, exit 3 unless the run was
    canceled (precedence 130 > 3 > 1 > 0).
- Test seam: `CliProgram.RunAsync` gains an optional `string? currentDirectory = null` (null = `Environment.CurrentDirectory`), passed to
  `RunCommand`; tests never change the process current directory.
- Cancellation (`Program.cs`): one `CancellationTokenSource`; `Console.CancelKeyPress`: the first Ctrl+C sets `e.Cancel = true` and cancels
  the source, a second one lets the process end; `PosixSignalRegistration.Create(PosixSignal.SIGTERM, c => { c.Cancel = true; cts.Cancel(); })`
  kept alive until exit. The token goes to `CliProgram.RunAsync`. A run that ends `Canceled` returns 130 after writing its report and
  summary; an `OperationCanceledException` before the run starts (loading) writes `canceled` to stderr and returns 130.

## Out of scope

Packaging (M6-73), docs (M6-74), colours or a `--quiet`/`--verbose` switch, printing script `console.log` lines.

## Acceptance criteria

- [ ] `tests/Yaapic.Cli.Tests/RunOutputTests.cs` (loopback server as in M6-70):
  1. a 3-request run (pass, failing test, send error) x 2 iterations: stdout lines in order, checked with regexes for the durations (the
     `Running`, two `Iteration` lines, three request lines each, the `x` line of the failed test, the summary);
  2. one iteration prints no `Iteration` line; a skipped request prints `SKIP` without a duration;
  3. `--reporter json --output <temp>/out/r.json`: the directory is created, the file parses with `DataNodeJson.Parse`, has
     `"schema": "yaapic-run/1"`, and stdout has the `Report (json):` line; exit code still 1 for a run with a failure;
  4. `--reporter junit` without `--output`: the file `RunReportJUnit.SuggestedFileName(result)` appears in the `currentDirectory` passed in
     and reads with `XmlReader`;
  5. `--output` pointing to an existing directory: exit 3 with the error line, also when the run itself failed;
  6. a token canceled during a `/delay/2000` request: exit 130, the report (if asked) is written with status `canceled`;
  7. a secret override value used in a failing test message appears as `••••` on stdout and in both reports.
- [ ] Ctrl+C checked by hand once on the published exe against a slow request (exit code 130); write the OS in the card's Notes.
- [ ] Architecture tests green, 0 warnings, all tests green; AOT publish of `Yaapic.Cli` has 0 IL2xxx/IL3xxx warnings.
- [ ] Docs: none in this card (M6-74).

## Suggested commit message

`feat(cli): print run progress, write reports and handle Ctrl+C`
