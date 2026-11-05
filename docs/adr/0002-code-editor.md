# ADR 0002: Code editor control

Status: Accepted

## Context

Bodies, scripts and Raw responses need a text editor with syntax highlighting, line numbers, folding and large-text
performance. The two App projects (WinUI 3, Avalonia) use different UI stacks.

## Options

1. **One native-stack editor per platform behind an abstraction:** AvaloniaEdit (with TextMate grammars) on Avalonia;
   a custom virtualised editor on WinUI 3, as no mature WinUI editor exists.
2. **Embed Monaco in a WebView.** Rich features, but a web runtime dependency, a JS bridge and a heavy startup cost.
3. **One custom-drawn editor shared by both stacks.** Most code to write and maintain; accessibility is hard.

## AOT risks

AvaloniaEdit is AOT compatible only with compiled bindings and without the reflection-based TextMate registry loader;
this must be checked when it is added. A WebView host adds a native runtime and an untyped JS bridge. A custom editor
carries no AOT risk.

## Decision

Option 1 as the working direction: the editor sits behind an interface in `Yaapic.UI.Abstractions`, with the
implementation in each App project. The final choice and its AOT verification belong to the card that introduces it.

## WinUI decision (M1-44, M2-69)

The `Yaapic.UI.Abstractions.Editing.ICodeEditor` interface (`Text`, `IsReadOnly`, `Language`, `TextChanged`) is in
place and `Yaapic.App.WinUI` implements it with `CodeEditorView`, the only consumer of the control, used by the request
body and the response body. M1-44 rejected `LoomEditor.WinUI` (it needs Windows App SDK 2.5.1 or later and a
`net11.0-windows10.0.26100` target) and shipped a `TextBox` fallback. M2-68 moved the app to Windows App SDK 2.6.5 and
the 26100 target, and M2-69 adopted `LoomEditor.WinUI` 0.4.0 inside `CodeEditorView`: JSON and XML colours from the
Yaapic syntax tokens (a VS Code theme JSON built in code per theme and reapplied on `ActualThemeChanged`), line numbers,
two-way text sync through `Model.ContentChanged` and `ResetText`. The `TextBox` fallback is removed.

Avalonia uses AvaloniaEdit (with its AOT check) in M2-70.
