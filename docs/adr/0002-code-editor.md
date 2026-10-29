# ADR 0002: Code editor control

Status: Proposed (confirmed by the first card that needs an editor, in M1)

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
