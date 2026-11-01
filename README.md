# yaapic

Yaapic is a cross-platform API client (HTTP first) built with C# and Native AOT. It ships two front-ends over one shared
core: **WinUI 3** (Windows) and **Avalonia** (Windows, macOS, Linux).

## Quick start (about 15 minutes)

Prerequisites:

- Git and the **.NET 11 SDK** pinned in [global.json](global.json) (a release candidate: install the exact version,
  it is picked up automatically).
- Native AOT publishing needs a native toolchain: Visual Studio "Desktop development with C++" on Windows,
  `clang` and `zlib1g-dev` on Linux, Xcode command line tools on macOS.
- The WinUI front-end builds on Windows only.

```bash
git clone https://github.com/Yaapic/yaapic.git
cd yaapic
dotnet build Yaapic.sln -c Release
dotnet test --solution Yaapic.sln -c Release
dotnet run --project src/Yaapic.App.WinUI -c Release
```

On macOS and Linux use the cross-platform solution filter and the Avalonia app instead:

```bash
dotnet build Yaapic.CrossPlatform.slnf -c Release
dotnet test --solution Yaapic.CrossPlatform.slnf -c Release
dotnet run --project src/Yaapic.App.Avalonia -c Release
```

The build must end with 0 warnings and 0 errors, and all tests must pass. To check what CI checks, also publish with
Native AOT and run the binary:

```bash
dotnet publish src/Yaapic.App.Avalonia -c Release -r <rid> -p:PublishAot=true -o publish
```

(`win-x64`, `linux-x64` or `osx-arm64`; use `src/Yaapic.App.WinUI` on Windows.) Any `IL2xxx` or `IL3xxx` warning is a
failure.

## Repository layout

| Path | Content |
|---|---|
| `src/Yaapic.Domain` | Plain models and enums, no dependencies |
| `src/Yaapic.Core` | Use cases and ports (HTTP, storage, variables) |
| `src/Yaapic.Infrastructure` | Port implementations (HTTP client, YAML workspace files) |
| `src/Yaapic.UI.Abstractions` | UI-facing interfaces (dialogs, code editor) |
| `src/Yaapic.ViewModels` | MVVM view models, shared by both front-ends |
| `src/Yaapic.Composition` | Wires the object graph for the App projects |
| `src/Yaapic.Design.Tokens` | Design token generator shared by the front-ends |
| `src/Yaapic.App.WinUI`, `src/Yaapic.App.Avalonia` | Front-ends: views only, no business logic |
| `tests/` | One test project per library, plus `Yaapic.Architecture.Tests` |
| `design/`, `tools/` | Design assets and tokens; screenshot tooling |
| `docs/` | [Architecture](docs/architecture.md), [ADRs](docs/adr), [dependencies](docs/dependencies.md), [file format](docs/file-format.md), [task cards](docs/tasks/README.md) |

## Architecture rules

Dependencies only point downwards; the table is in [docs/architecture.md](docs/architecture.md) and is enforced by
`tests/Yaapic.Architecture.Tests`. In short: `Domain` depends on nothing, `Core` only on `Domain`, `ViewModels` never on
`Infrastructure` or an App project, and App projects hold no business logic. Other rules:

- No reflection, `dynamic` or `Expression.Compile` (Native AOT); JSON only through `System.Text.Json` source generators.
- A new third-party package needs a justification in [docs/dependencies.md](docs/dependencies.md) first.
- Code, comments and docs are in English. Every public type and member has an XML doc comment (CS1591 is an error).
- Formatting is checked in CI with `dotnet format --verify-no-changes`.

## Work is split into task cards

Each unit of work is a card in [docs/tasks](docs/tasks/README.md) with a goal, design decisions, acceptance criteria
and a suggested commit message. The index lists status and dependencies.

With Claude Code, the `/next-task` skill ([.claude/skills/next-task](.claude/skills/next-task/SKILL.md)) picks the
first `todo` card whose dependencies are `done` (or the one you name, e.g. `/next-task M1-28`), implements it
tests-first, runs the build, tests and AOT checks, ticks the card, updates the index and makes one commit. Cards are
done on a single branch per phase, not one branch per card. Commits are GPG-signed and pushed.

## Continuous integration and dependencies

GitHub Actions ([.github/workflows/ci.yml](.github/workflows/ci.yml)) builds, tests, publishes with Native AOT and
smoke-runs the binary on Windows, Linux and macOS for every pull request. Dependabot is enabled
([.github/dependabot.yml](.github/dependabot.yml)): weekly NuGet and GitHub Actions updates. The .NET SDK itself is
pinned on purpose and not updated automatically.

## License

See [LICENSE](LICENSE).
