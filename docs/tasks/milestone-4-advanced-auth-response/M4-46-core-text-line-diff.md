# M4-46: Core: line diff (Myers)

Status: done
Platform: any
Depends on: M4-04

## Goal

A pure, tested line-diff algorithm.

## Design decisions

- Read first: ADR 0015 (Body comparison). Namespace `Yaapic.Core.Diff`, folder `src/Yaapic.Core/Diff/`.
- `public enum DiffOp { Equal, Added, Removed }`; `public sealed record DiffLine(DiffOp Op, string Text, int? OldLine, int? NewLine)` (1-based line
  numbers; `Added` has no old number, `Removed` no new number).
- `public static class LineDiff`: `static IReadOnlyList<DiffLine> Compute(IReadOnlyList<string> oldLines, IReadOnlyList<string> newLines,
  bool ignoreWhitespace = false)`.
  - Myers `O(ND)` shortest edit script with the usual trace, then backtracking; deterministic: when a choice exists, prefer `Removed` before
    `Added` in a changed block (so output reads like `git diff`).
  - `ignoreWhitespace` compares lines after `Trim()` but emits the **original** text (taken from the new side for `Equal`).
  - Guard: when `oldLines.Count + newLines.Count` exceeds 20 000 the method throws `ArgumentException` (the caller checks sizes first, ADR 0015
    limits bodies to 1 MiB).
- `static IReadOnlyList<string> SplitLines(string text)`: splits on `\r\n`, `\n`, `\r`; a trailing newline does not produce an extra empty line;
  empty text gives an empty list.
- Helper `static DiffStats Summarize(IReadOnlyList<DiffLine>)` returning `record DiffStats(int Added, int Removed, bool HasChanges)`.

## Out of scope

Headers, JSON awareness, folding of context (VM), UI.

## Acceptance criteria

- [x] Tests in `Yaapic.Core.Tests`: identical inputs; empty vs non-empty both ways; one changed line; insertion and deletion at start, middle, end;
  a classic textbook example (`ABCABBA` vs `CBABAC` lines gives an edit script of length 5); reordered blocks; duplicated lines; ignore-whitespace
  true and false; CRLF vs LF split; line numbers consistent (replaying the script on the old lines reproduces the new lines: a property-style test over a
  few hundred random small inputs with a fixed seed); size guard; `Summarize`.
- [x] Architecture tests green, 0 warnings, XML docs, all tests green.

## Suggested commit message

`feat(core): add a Myers line diff`
