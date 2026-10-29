# M0-04: Design tokens generator and snapshot test

Status: todo
Platform: any
Depends on: M0-02

## Goal

Generate the WinUI `ResourceDictionary` and the Avalonia styles from `design/tokens/yaapic.tokens.json` at build time
(`Yaapic.Design.Tokens`).

## Design decisions

- Source of truth: `design/tokens/yaapic.tokens.json` (DTCG). `design/xaml/` is the reference output.
- Keys follow `Yaapic<Name><Type>` (for example `YaapicAccentFillBrush`).
- Themes: dark (default), light, high contrast (system colors). Only the semantic layer changes per theme.
- The generator must be AOT-safe (System.Text.Json source generation).

## Out of scope

Control styles, fonts, icons.

## Acceptance criteria

- [ ] Generated output matches the reference files in `design/xaml/` (snapshot test).
- [ ] Changing a token changes the output and fails the snapshot test until it is updated.

## Suggested commit message

`feat: generate WinUI and Avalonia resources from design tokens`
