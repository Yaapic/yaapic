# ADR 0006: Shared design tokens

Status: Accepted

## Context

Two UI stacks (WinUI 3 and Avalonia) must share one visual identity and stay in sync, with light, dark and high
contrast themes.

## Options

1. **One DTCG token file (`design/tokens/yaapic.tokens.json`) and an in-repo generator (`Yaapic.Design.Tokens`) that
   emits the WinUI and Avalonia resource dictionaries**, guarded by a snapshot test.
2. **Hand-maintained XAML dictionaries per platform.** Drift is guaranteed.
3. **Style Dictionary (Node) in the build.** Adds a Node toolchain to a .NET repository.

## AOT risks

None for the app: the generator runs at build time only and the generated XAML is plain resources.

## Decision

Option 1. The token file is the single source of truth; generated dictionaries are never edited by hand and a snapshot
test fails when they drift.
