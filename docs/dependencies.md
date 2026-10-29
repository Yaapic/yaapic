# Dependencies

Every third-party dependency needs a written justification here before it is added to `Directory.Packages.props`.

| Package | Version | Used by | Justification |
|---|---|---|---|
| xunit.v3 | 4.0.2 | Yaapic.Architecture.Tests | Test framework with native Microsoft.Testing.Platform support, required by `dotnet test --solution`. Test-only, never shipped. |
| Microsoft.WindowsAppSDK | 1.8.260921001 | Yaapic.App.WinUI | WinUI 3 runtime and XAML compiler; first-party, Native AOT supported (1.6+). |
| Microsoft.Windows.SDK.BuildTools | 10.0.26100.7463 | Yaapic.App.WinUI | Build-time tooling required by Windows App SDK projects. Not shipped. |

## Architecture tests: NetArchTest

NetArchTest was not adopted. The dependency rules are project-level, so `Yaapic.Architecture.Tests` checks the
`ProjectReference` items of each csproj directly (an unused reference is not emitted into the assembly, so a type-level
check would miss it). No extra package and no reflection-based tooling are needed. NetArchTest compatibility with
Native AOT was not evaluated; revisit if type-level rules (namespaces, naming) are needed.
