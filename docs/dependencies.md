# Dependencies

Every third-party dependency needs a written justification here before it is added to `Directory.Packages.props`.

| Package | Version | Used by | Justification |
|---|---|---|---|
| xunit.v3 | 4.0.2 | Yaapic.Architecture.Tests | Test framework with native Microsoft.Testing.Platform support, required by `dotnet test --solution`. Test-only, never shipped. |

## Architecture tests: NetArchTest

NetArchTest was not adopted. The dependency rules are project-level, so `Yaapic.Architecture.Tests` checks the
`ProjectReference` items of each csproj directly (an unused reference is not emitted into the assembly, so a type-level
check would miss it). No extra package and no reflection-based tooling are needed. NetArchTest compatibility with
Native AOT was not evaluated; revisit if type-level rules (namespaces, naming) are needed.
