# Dependencies

Every third-party dependency needs a written justification here before it is added to `Directory.Packages.props`.

| Package | Version | Used by | Justification |
|---|---|---|---|
| xunit.v3 | 4.0.2 | Yaapic.Architecture.Tests | Test framework with native Microsoft.Testing.Platform support, required by `dotnet test --solution`. Test-only, never shipped. |
| VYaml | 1.5.0 | Yaapic.Infrastructure | YAML serialisation for the workspace file format (ADR 0005). MIT, source-generated `[YamlObject]` formatters, no reflection, Native AOT. Verified in M1-10, see below. |
| CommunityToolkit.Mvvm | 8.4.2 | Yaapic.ViewModels | `ObservableObject`, `[ObservableProperty]` and `[RelayCommand]` source generators for ViewModels. MIT, first-party .NET Foundation; generated code is reflection-free on the paths we use (`ObservableValidator` and `Messenger.RegisterAll` are forbidden). |
| Microsoft.WindowsAppSDK | 1.8.260921001 | Yaapic.App.WinUI | WinUI 3 runtime and XAML compiler; first-party, Native AOT supported (1.6+). |
| Microsoft.Windows.SDK.BuildTools | 10.0.26100.7463 | Yaapic.App.WinUI | Build-time tooling required by Windows App SDK projects. Not shipped. |

## Architecture tests: NetArchTest

NetArchTest was not adopted. The dependency rules are project-level, so `Yaapic.Architecture.Tests` checks the
`ProjectReference` items of each csproj directly (an unused reference is not emitted into the assembly, so a type-level
check would miss it). No extra package and no reflection-based tooling are needed. NetArchTest compatibility with
Native AOT was not evaluated; revisit if type-level rules (namespaces, naming) are needed.

## VYaml: Native AOT check (M1-10)

VYaml 1.5.0 (latest stable on nuget.org at the time). A scratch console project (not committed) referenced
`Yaapic.Infrastructure` and round-tripped a `[YamlObject]` type holding a string, a nested `[YamlObject]`, a `List<T>`
of objects and a multi-line string. `dotnet publish -c Release -r win-x64 -p:PublishAot=true` produced **0 IL2xxx/IL3xxx
warnings**, and the published binary ran and the round trip matched.

Multi-line strings are emitted as **literal block scalars** (`|`), so scripts and bodies stay readable in diffs.
Property names are emitted in camelCase by default (`openRequests`), matching the file format.

## Bundled assets

| Asset | Version | Licence | Used by | Source | Justification |
|---|---|---|---|---|---|
| JetBrains Mono (variable) | 2.304 | SIL OFL 1.1 (`Assets/Fonts/OFL.txt`) | Yaapic.App.WinUI | <https://github.com/JetBrains/JetBrainsMono/releases/tag/v2.304> | Code font of the design system (`type.code`): URLs, headers and bodies are always monospace. |
