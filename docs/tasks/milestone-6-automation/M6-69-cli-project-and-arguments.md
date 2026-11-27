# M6-69: CLI: `Yaapic.Cli` project and argument parser

Status: todo
Platform: any
Depends on: M6-65

## Goal

Create the `yaapic-cli` executable (Native AOT) with its hand-written argument parser, `--help`, `--version` and the usage exit code, so
M6-70 only has to add the run itself.

## Design decisions

- Read first: ADR 0022 (options table, exit codes), M6-62, `src/Yaapic.App.Avalonia/Yaapic.App.Avalonia.csproj` and `Directory.Build.props`
  (shared properties, `YaapicVersion`), `tests/Yaapic.Core.Tests/Yaapic.Core.Tests.csproj` (test project shape),
  `tests/Yaapic.Architecture.Tests/DependencyRulesTests.cs`, `src/Yaapic.Composition/AppComposition.CreateAppInfo`.
- `src/Yaapic.Cli/Yaapic.Cli.csproj`: `OutputType` `Exe`, `TargetFramework` as the other projects, `AssemblyName` `yaapic-cli`,
  `RootNamespace` `Yaapic.Cli`, `UseOptimizedPublish` `false` (same comment as the apps), `InternalsVisibleTo` `Yaapic.Cli.Tests`. Project
  references: `Yaapic.Composition`, `Yaapic.Core`, `Yaapic.Domain`, `Yaapic.Infrastructure`, `Yaapic.Localization`. No package. Native AOT is switched on at publish
  time with `-p:PublishAot=true`, as for the apps.
- `tests/Yaapic.Cli.Tests` (xUnit, same shape as `Yaapic.Core.Tests`). Add both projects to `Yaapic.sln` and `Yaapic.CrossPlatform.slnf`.
- `DependencyRulesTests.AllowedReferences`: `["Yaapic.Cli"] = ["Yaapic.Composition", "Yaapic.Core", "Yaapic.Domain", "Yaapic.Infrastructure", "Yaapic.Localization"]`.
- English only (ADR 0022): the first line of `CliProgram.RunAsync` calls `Localizer.Use(<the English member of UiLanguage>)`, so the Core
  messages the CLI prints (data file, import reader, script errors) are English whatever the OS culture.
- Types are `internal` (an exe; no public API, so no XML doc obligation, but keep short summaries on each type):
  - `enum CliExitCode { Success = 0, Failures = 1, Usage = 2, Input = 3, Internal = 4, Canceled = 130 }`;
  - `enum CliReporter { Json, JUnit }`;
  - `sealed record RunArguments(string Workspace, string Folder, string? Environment, int? Iterations, string? DataFile, int DelayMs,
    bool StopOnFailure, CliReporter? Reporter, string? Output)`;
  - `sealed record CliParseResult(CliCommandKind Kind, RunArguments? Run, string? Error)` with `enum CliCommandKind { Help, Version, Run, Error }`;
  - `static class CliArgumentParser { public static CliParseResult Parse(IReadOnlyList<string> args) }`;
  - `static class CliProgram { public static Task<int> RunAsync(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr,
    IReadOnlyDictionary<string, string?> environment, CancellationToken ct) }`;
  - `Program.cs`: top-level statements calling `CliProgram.RunAsync(args, Console.Out, Console.Error, <Environment.GetEnvironmentVariables()
    copied into a dictionary>, CancellationToken.None)` and returning its code.
- Parsing rules (ADR 0022):
  - `--help` or `-h` anywhere: `Help` (wins over every error). `--version` anywhere (and no help): `Version`.
  - First argument must be `run`; then exactly one positional `<workspace>`; options in any order before or after it.
  - `--name value` or `--name=value`; a value that starts with `--` counts as missing. `--stop-on-failure` is a flag.
  - Integers: invariant culture, digits only (`int.TryParse` with `NumberStyles.None`), range checked here.
  - `--iterations` stays `null` when absent (M6-70 defaults it from the data file).
- Error texts (written to stderr as `error: <text>` then `Run 'yaapic-cli --help' for usage.`, exit 2), exactly:
  no argument: `missing command (expected 'run')`; `unknown command '{0}'`; `missing <workspace>`; `unexpected argument '{0}'`;
  `unknown option '{0}'`; `option '{0}' needs a value`; `option '{0}' takes no value`; `option '{0}' is given twice`; `missing --folder`;
  `--iterations must be a whole number from 1 to 10000`; `--delay must be a whole number from 0 to 60000`;
  `--reporter must be junit or json`; `--output needs --reporter`.
- `--version` prints `yaapic-cli {version}` (the version of `AppComposition.CreateAppInfo()`), exit 0.
- `--help` prints to stdout, exit 0, exactly (with `{version}` replaced):
  ```text
  yaapic-cli {version}: run Yaapic folders from the command line.

  Usage:
    yaapic-cli run <workspace> --folder <path> [options]
    yaapic-cli --help | --version

  Options:
    --folder <path>        Folder to run, e.g. "Collection/Sub" (required)
    --environment <name>   Environment to use (default: none)
    --iterations <n>       1 to 10000 (default: the data row count, else 1)
    --data <file>          CSV or JSON iteration data file
    --delay <ms>           Delay between two requests, 0 to 60000 (default: 0)
    --stop-on-failure      Stop at the first failed or errored request
    --reporter <kind>      junit or json
    --output <file>        Report file (needs --reporter; default: a generated name in the current directory)

  Secrets are not read from the vault: set YAAPIC_VAR_<name> to give variable <name> a value.

  Exit codes: 0 passed, 1 failures, 2 usage error, 3 input or I/O error, 4 unexpected error, 130 canceled.
  ```
- Until M6-70, a valid `run` writes `error: the run command is not implemented yet` to stderr and returns 4 (M6-70 replaces it; the test
  for it is deleted there).
- Any exception escaping `RunAsync` is caught in `Program.cs`: `error: {message}` on stderr, exit 4.

## Out of scope

The run (M6-70), output and reports (M6-71), packaging (M6-73), docs (M6-74).

## Acceptance criteria

- [ ] `tests/Yaapic.Cli.Tests/CliArgumentParserTests.cs`: a full valid line (every option, both `--x v` and `--x=v` forms, options before
  and after the workspace); each error text above (one test case each, `[Theory]`); `-h` with otherwise invalid arguments gives `Help`;
  `--version`; `--iterations 0`, `10001`, `-1`, `1e3`, `abc`; `--delay 60001`; `--reporter JUNIT` (error: values are lower case);
  `--output` without `--reporter`.
- [ ] `tests/Yaapic.Cli.Tests/CliProgramTests.cs` with `StringWriter`s: `--help` text and exit 0; `--version` exit 0; a usage error writes
  the two stderr lines and returns 2; nothing is written to stdout on an error.
- [ ] `dotnet publish src/Yaapic.Cli -c Release -r <host rid> -p:PublishAot=true -o artifacts/cli`: 0 IL2xxx/IL3xxx warnings; the published
  `yaapic-cli --version` prints the version and exits 0; the folder holds the QuickJS-NG and Lua native libraries (they come from
  `Yaapic.Scripting`); write the RID used in the card's Notes.
- [ ] Architecture tests green (the new rule included), 0 warnings, all tests green.
- [ ] Docs: `docs/architecture.md`, table "Projects and allowed references": add the row
  `| Yaapic.Cli | Composition, Core, Domain, Infrastructure, Localization; never an App project or ViewModels directly |`. README "Repository layout": add
  `src/Yaapic.Cli` with one line ("the `yaapic-cli` command-line runner, ADR 0022") if that section lists the src projects.

## Suggested commit message

`feat(cli): add the yaapic-cli project and argument parser`
