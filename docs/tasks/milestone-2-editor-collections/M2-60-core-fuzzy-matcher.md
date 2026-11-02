# M2-60: Core: fuzzy matcher

Status: todo
Platform: any
Depends on: M2-01

## Goal

A deterministic fuzzy matcher for the command palette.

## Design decisions

- `public static class FuzzyMatcher` in `Yaapic.Core.Search` with `FuzzyMatch? Match(string query, string text)` and
  `public sealed record FuzzyMatch(int Score, IReadOnlyList<int> Indices)` (`Indices` = positions in `text` of the matched
  characters, ascending).
- A blank query (null, empty or whitespace) matches everything with score 0 and no indices. Otherwise the query is
  trimmed and every character must appear in `text` in order, compared case-insensitively (ordinal ignore case); if not,
  the result is null.
- Greedy left-to-right: each query character takes the earliest occurrence after the previous match. Score = sum over
  matched characters of: 1; **+8** when at index 0; **+6** when the character starts a word (the previous character is
  one of space, `-`, `_`, `/`, `.`, `:` or the character is upper case after a lower case one); **+4** when it directly
  follows the previous matched character; minus 1 for every skipped character between two consecutive matches.
  The first match's leading offset is not penalised.
- Ranking helper `int Compare(FuzzyMatch a, string textA, FuzzyMatch b, string textB)`: higher score first, then shorter
  text, then ordinal text; used by the palette.
- Pure functions, no allocation beyond the indices list, no culture-sensitive comparison.

## Out of scope

Typo tolerance, searching several fields (the palette decides what to match), highlighting UI.

## Acceptance criteria

- [ ] Tests in `Yaapic.Core.Tests`: blank query; non-subsequence returns null; case-insensitivity; indices correct;
      a prefix match beats a mid-word match; a word-start match (`gr` in `Get Request`... `New Request` with `nr`) beats a
      scattered one; consecutive beats gapped; exact numeric score of three small hand-computed cases (`"ab"` on `"ab"`,
      `"ab"` on `"a-b"`, `"ac"` on `"abc"`); `Compare` tie-breaks.
- [ ] 0 warnings.

## Suggested commit message

`feat(core): add fuzzy matcher for the command palette`
